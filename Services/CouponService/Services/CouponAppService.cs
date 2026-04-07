using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Exceptions;
using CouponService.Infrastructure.Observability;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CouponService.Services;

public class CouponAppService(
    ICouponRepository couponRepository,
    IUserCouponRepository userCouponRepository,
    ICouponUsageRepository couponUsageRepository,
    IOutboxEventRepository outboxEventRepository,
    ICouponRuleEngine ruleEngine,
    ICouponCacheService cacheService,
    TimeProvider timeProvider,
    ILogger<CouponAppService> logger) : ICouponService
{
    public async Task<PagedResponse<CouponSummaryResponse>> GetAvailableCouponsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var coupons = await couponRepository.GetAvailableCouponsAsync(now, page, pageSize, ct);
        var totalCount = await couponRepository.CountAvailableCouponsAsync(now, ct);

        var items = coupons.Select(c => new CouponSummaryResponse(
            c.Id, c.Code, (int)c.DiscountType, c.DiscountValue,
            c.MaxDiscountAmount, c.ValidFrom, c.ValidUntil, c.IsActive)).ToList();

        return new PagedResponse<CouponSummaryResponse>(
            items, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<CouponResponse?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(id, ct);
        return coupon is null ? null : MapToResponse(coupon);
    }

    public async Task<CouponResponse> CreateCouponAsync(
        CreateCouponRequest request, CancellationToken ct = default)
    {
        var coupon = new Coupon
        {
            Code = request.Code,
            CouponTypeId = request.CouponTypeId,
            CampaignId = request.CampaignId,
            DiscountType = (DiscountType)request.DiscountType,
            DiscountValue = request.DiscountValue,
            MaxDiscountAmount = request.MaxDiscountAmount,
            MinOrderAmount = request.MinOrderAmount,
            MaxUsageCount = request.MaxUsageCount,
            MaxUsagePerUser = request.MaxUsagePerUser,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil
        };

        await couponRepository.AddAsync(coupon, ct);
        await couponRepository.SaveChangesAsync(ct);

        logger.LogInformation("クーポン作成: {CouponCode}, Id={CouponId}", coupon.Code, coupon.Id);
        return MapToResponse(coupon);
    }

    public async Task<CouponResponse> UpdateCouponAsync(
        string id, UpdateCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(id, ct)
            ?? throw new CouponNotFoundException($"クーポン {id} が見つかりません");

        if (request.CouponTypeId is not null) coupon.CouponTypeId = request.CouponTypeId;
        if (request.CampaignId is not null) coupon.CampaignId = request.CampaignId;
        if (request.DiscountType.HasValue) coupon.DiscountType = (DiscountType)request.DiscountType.Value;
        if (request.DiscountValue.HasValue) coupon.DiscountValue = request.DiscountValue.Value;
        if (request.MaxDiscountAmount.HasValue) coupon.MaxDiscountAmount = request.MaxDiscountAmount;
        if (request.MinOrderAmount.HasValue) coupon.MinOrderAmount = request.MinOrderAmount.Value;
        if (request.MaxUsageCount.HasValue) coupon.MaxUsageCount = request.MaxUsageCount.Value;
        if (request.MaxUsagePerUser.HasValue) coupon.MaxUsagePerUser = request.MaxUsagePerUser.Value;
        if (request.ValidFrom.HasValue) coupon.ValidFrom = request.ValidFrom.Value;
        if (request.ValidUntil.HasValue) coupon.ValidUntil = request.ValidUntil.Value;
        if (request.IsActive.HasValue) coupon.IsActive = request.IsActive.Value;

        try
        {
            await couponRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: Coupon {CouponId}", id);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
        }

        await cacheService.InvalidateAsync(coupon.Code, ct);
        logger.LogInformation("クーポン更新: {CouponCode}", coupon.Code);
        return MapToResponse(coupon);
    }

    public async Task DeactivateCouponAsync(string id, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(id, ct)
            ?? throw new CouponNotFoundException($"クーポン {id} が見つかりません");

        coupon.IsActive = false;
        await couponRepository.SaveChangesAsync(ct);
        await cacheService.InvalidateAsync(coupon.Code, ct);

        logger.LogInformation("クーポン無効化: {CouponCode}", coupon.Code);
    }

    public async Task<UserCouponResponse> AcquireCouponAsync(
        string code, string userId, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByCodeAsync(code, ct)
            ?? throw new CouponNotFoundException($"クーポン {code} が見つかりません");

        if (!coupon.IsActive)
            throw new InvalidCouponException("クーポンが無効です");

        var now = timeProvider.GetUtcNow();
        if (now < coupon.ValidFrom || now > coupon.ValidUntil)
            throw new CouponExpiredException("クーポンの有効期間外です");

        var existing = await userCouponRepository.FindByUserAndCouponAsync(userId, coupon.Id, ct);
        if (existing is not null)
            throw new CouponAlreadyAcquiredException("このクーポンは既に取得済みです");

        var userCoupon = new UserCoupon
        {
            UserId = userId,
            CouponId = coupon.Id
        };

        await userCouponRepository.AddAsync(userCoupon, ct);
        await userCouponRepository.SaveChangesAsync(ct);

        logger.LogInformation("クーポン取得: {CouponCode}, UserId={UserId}", code, userId);

        return new UserCouponResponse(
            userCoupon.Id, coupon.Code, (int)coupon.DiscountType,
            coupon.DiscountValue, coupon.MaxDiscountAmount,
            coupon.ValidFrom, coupon.ValidUntil, UserCouponStatus.Available.ToString(), userCoupon.AcquiredAt);
    }

    public async Task<List<UserCouponResponse>> GetUserCouponsAsync(
        string userId, CancellationToken ct = default)
    {
        var userCoupons = await userCouponRepository.GetByUserIdAsync(userId, page: 1, pageSize: 50, ct);
        return userCoupons.Select(uc => new UserCouponResponse(
            uc.Id, uc.Coupon?.Code ?? string.Empty,
            (int)(uc.Coupon?.DiscountType ?? 0), uc.Coupon?.DiscountValue ?? 0m,
            uc.Coupon?.MaxDiscountAmount,
            uc.Coupon?.ValidFrom ?? default, uc.Coupon?.ValidUntil ?? default,
            uc.Status.ToString(), uc.AcquiredAt)).ToList();
    }

    public async Task<ValidationResult> ValidateAndApplyAsync(
        string couponCode, string userId, decimal orderAmount,
        string? categoryId, CancellationToken ct = default)
        => await ruleEngine.ValidateAsync(couponCode, userId, orderAmount, categoryId, ct);

    public async Task<CouponUsageResponse> RedeemCouponAsync(
        string userId, RedeemCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(request.CouponId, ct)
            ?? throw new CouponNotFoundException($"クーポン {request.CouponId} が見つかりません");

        // Check idempotency
        var existingUsage = await couponUsageRepository.FindByOrderIdAsync(coupon.Id, request.OrderId, ct);
        if (existingUsage is not null)
            return new CouponUsageResponse(existingUsage.Id, existingUsage.UserId,
                existingUsage.OrderId, existingUsage.DiscountAmount, existingUsage.UsedAt);

        // TOCTOU 防止: Redeem 前にクーポンの有効性を再検証
        if (!coupon.IsActive)
            throw new InvalidCouponException("クーポンが無効です");

        var now = timeProvider.GetUtcNow();
        if (now < coupon.ValidFrom || now > coupon.ValidUntil)
            throw new CouponExpiredException("クーポンの有効期間外です");

        if (coupon.CurrentUsageCount >= coupon.MaxUsageCount)
            throw new CouponUsageLimitExceededException("クーポンの利用上限に達しました");

        // サーバー側で割引額を再計算
        var serverDiscount = CouponRuleEngine.CalculateDiscount(coupon, request.DiscountAmount);

        coupon.CurrentUsageCount++;

        var usage = new CouponUsage
        {
            CouponId = coupon.Id,
            UserId = userId,
            OrderId = request.OrderId,
            DiscountAmount = serverDiscount
        };

        await couponUsageRepository.AddAsync(usage, ct);

        CouponMetrics.RedeemedTotal.Add(1);
        CouponMetrics.DiscountAmount.Record((double)serverDiscount);

        // Outbox event（型付き Event record を使用）
        var outboxEvent = new OutboxEvent
        {
            AggregateType = "Coupon",
            AggregateId = coupon.Id,
            EventType = "coupon.applied",
            Payload = JsonSerializer.Serialize(new Consumers.CouponAppliedEvent(
                coupon.Id, coupon.Code, userId, request.OrderId,
                serverDiscount, timeProvider.GetUtcNow()))
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);

        try
        {
            await couponRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: Coupon {CouponId}", coupon.Id);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
        }

        await cacheService.InvalidateAsync(coupon.Code, ct);
        logger.LogInformation("クーポン利用確定: {CouponCode}, OrderId={OrderId}", coupon.Code, request.OrderId);

        return new CouponUsageResponse(usage.Id, userId, request.OrderId,
            serverDiscount, usage.UsedAt);
    }

    public async Task ReleaseCouponAsync(
        ReleaseCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(request.CouponId, ct)
            ?? throw new CouponNotFoundException($"クーポン {request.CouponId} が見つかりません");

        var usage = await couponUsageRepository.FindByOrderIdAsync(coupon.Id, request.OrderId, ct);
        if (usage is null || usage.IsReleased)
        {
            logger.LogInformation(
                "リリース済みまたは対象なし（冪等）: CouponId={CouponId}, OrderId={OrderId}",
                request.CouponId, request.OrderId);
            return; // Idempotent: already released or not found
        }

        usage.IsReleased = true;
        usage.ReleasedAt = timeProvider.GetUtcNow();
        coupon.CurrentUsageCount = Math.Max(0, coupon.CurrentUsageCount - 1);

        // Outbox event（型付き Event record を使用）
        var outboxEvent = new OutboxEvent
        {
            AggregateType = "Coupon",
            AggregateId = coupon.Id,
            EventType = "coupon.released",
            Payload = JsonSerializer.Serialize(new Consumers.CouponReleasedEvent(
                coupon.Id, coupon.Code, usage.UserId, request.OrderId,
                usage.DiscountAmount, timeProvider.GetUtcNow()))
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);

        try
        {
            await couponRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: Coupon {CouponId}", coupon.Id);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
        }

        await cacheService.InvalidateAsync(coupon.Code, ct);
        logger.LogInformation("クーポンリリース: {CouponCode}, OrderId={OrderId}", coupon.Code, request.OrderId);
    }

    public async Task<CalculateDiscountResponse> CalculateDiscountAsync(
        CalculateDiscountRequest request, CancellationToken ct = default)
    {
        var result = await ruleEngine.ValidateAsync(
            request.CouponCode, request.UserId, request.OrderAmount, request.CategoryId, ct);

        if (!result.IsValid)
            return new CalculateDiscountResponse(false, result.ErrorCode, result.ErrorMessage,
                null, 0m, 0, 0m);

        // 二重クエリ解消: ValidationResult から直接取得
        return new CalculateDiscountResponse(true, null, null,
            result.CouponId, result.DiscountAmount,
            result.DiscountType ?? 0, result.DiscountValue ?? 0m);
    }

    public async Task<PagedResponse<CouponResponse>> GetAllCouponsAsync(
        CouponQueryParams query, CancellationToken ct = default)
    {
        var coupons = await couponRepository.GetAllCouponsAsync(query, ct);
        var totalCount = await couponRepository.CountAllCouponsAsync(ct);

        var items = coupons.Select(MapToResponse).ToList();
        return new PagedResponse<CouponResponse>(
            items, totalCount, query.Page, query.PageSize,
            (int)Math.Ceiling((double)totalCount / query.PageSize));
    }

    public async Task<PagedResponse<CouponUsageResponse>> GetCouponUsagesAsync(
        string couponId, int page, int pageSize, CancellationToken ct = default)
    {
        var usages = await couponUsageRepository.GetByCouponIdAsync(couponId, page, pageSize, ct);
        var totalCount = await couponUsageRepository.CountByCouponIdAsync(couponId, ct);

        var items = usages.Select(u => new CouponUsageResponse(
            u.Id, u.UserId, u.OrderId, u.DiscountAmount, u.UsedAt)).ToList();

        return new PagedResponse<CouponUsageResponse>(
            items, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<int> DeactivateExpiredCouponsAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var deactivated = await couponRepository.DeactivateExpiredCouponsAsync(now, ct);
        if (deactivated > 0)
            logger.LogInformation("期限切れクーポンを {Count} 件無効化しました", deactivated);
        return deactivated;
    }

    private static CouponResponse MapToResponse(Coupon c) => new(
        c.Id, c.Code, c.CouponTypeId, c.CampaignId,
        (int)c.DiscountType, c.DiscountValue, c.MaxDiscountAmount,
        c.MinOrderAmount, c.MaxUsageCount, c.CurrentUsageCount,
        c.MaxUsagePerUser, c.ValidFrom, c.ValidUntil, c.IsActive, c.CreatedAt);
}
