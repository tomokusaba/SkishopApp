using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PointService.Configurations;
using PointService.DTOs.Requests;
using PointService.DTOs.Responses;
using PointService.Events;
using PointService.Exceptions;
using PointService.Models;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class PointManagementService(
    IPointAccountRepository accountRepository,
    IPointTransactionRepository transactionRepository,
    IPointExpiryRepository expiryRepository,
    IPointAuditLogRepository auditLogRepository,
    IOutboxEventRepository outboxEventRepository,
    IPointCalculator calculator,
    IPointCacheService cacheService,
    ITierService tierService,
    IOptions<PointSettings> options,
    TimeProvider timeProvider,
    ILogger<PointManagementService> logger) : IPointService
{
    private readonly PointSettings _settings = options.Value;

    public async Task<EarnPointsResult> EarnPointsAsync(
        string userId, string orderId, decimal orderAmount,
        CancellationToken ct = default)
    {
        var existingTx = await transactionRepository.FindByReferenceAsync(orderId, TransactionTypes.Earn, ct);
        if (existingTx is not null)
        {
            logger.LogInformation("冪等性: Earn 既に処理済み。OrderId={OrderId}", orderId);
            var existingAccount = await accountRepository.FindByUserIdAsync(userId, ct);
            return new EarnPointsResult(existingTx.Points, existingAccount?.AvailablePoints ?? 0);
        }

        var account = await accountRepository.FindByUserIdAsync(userId, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var pointRate = await tierService.GetPointRateForUserAsync(userId, ct);
        var earnedPoints = calculator.Calculate(orderAmount, pointRate, 1.0m);

        account.EarnPoints(earnedPoints);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMonths(_settings.ExpirationMonths);

        var userIdGuid = Guid.Parse(userId);

        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Type = TransactionTypes.Earn,
            Points = earnedPoints,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = orderId,
            ReferenceType = ReferenceTypes.Order,
            Description = $"注文 {orderId} のポイント付与",
            ExpiresAt = expiresAt
        }, ct);

        await expiryRepository.AddAsync(new PointExpiry
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Points = earnedPoints,
            ExpiresAt = expiresAt
        }, ct);

        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            AggregateType = AggregateTypes.PointAccount,
            AggregateId = account.Id,
            EventType = EventTopics.PointEarned,
            Payload = JsonSerializer.Serialize(new PointsEarnedEvent(
                userId, earnedPoints, orderId,
                account.AvailablePoints, "", now))
        }, ct);

        try
        {
            await accountRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: UserId={UserId}", userId);
            throw new Exceptions.ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。", ex);
        }

        await cacheService.InvalidateBalanceCacheAsync(userId, ct);
        return new EarnPointsResult(earnedPoints, account.AvailablePoints);
    }

    public async Task<ReservePointsResult> ReservePointsAsync(
        ReservePointsRequest request, CancellationToken ct = default)
    {
        var existingTx = await transactionRepository.FindByReferenceAsync(
            request.OrderId, TransactionTypes.Reserve, ct);
        if (existingTx is not null)
        {
            logger.LogInformation("冪等性: Reserve 既に処理済み。OrderId={OrderId}", request.OrderId);
            var currentAccount = await accountRepository.FindByUserIdAsync(request.UserId, ct);
            return new ReservePointsResult(true, currentAccount?.AvailablePoints ?? 0);
        }

        var account = await accountRepository.FindByUserIdAsync(request.UserId, ct)
            ?? throw new PointAccountNotFoundException(request.UserId);

        if (account.AvailablePoints < request.Points)
            return new ReservePointsResult(false, account.AvailablePoints, "ポイント残高不足");

        await ConsumePointsFifoAsync(account, request.Points, ct);

        account.ReservePoints(request.Points);

        var userIdGuid = Guid.Parse(request.UserId);
        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Type = TransactionTypes.Reserve,
            Points = -request.Points,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = request.OrderId,
            ReferenceType = ReferenceTypes.Order,
            Description = $"注文 {request.OrderId} のポイント仮消費"
        }, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            AggregateType = AggregateTypes.PointAccount,
            AggregateId = account.Id,
            EventType = EventTopics.PointReserved,
            Payload = JsonSerializer.Serialize(new PointsReservedEvent(
                request.UserId, request.Points, request.OrderId, "", now))
        }, ct);

        try
        {
            await accountRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: UserId={UserId}", request.UserId);
            throw new Exceptions.ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。", ex);
        }

        await cacheService.InvalidateBalanceCacheAsync(request.UserId, ct);
        return new ReservePointsResult(true, account.AvailablePoints);
    }

    public async Task<int> ConfirmPointsAsync(
        string userId, string orderId, CancellationToken ct = default)
    {
        var existingTx = await transactionRepository.FindByReferenceAsync(orderId, TransactionTypes.Redeem, ct);
        if (existingTx is not null)
        {
            logger.LogInformation("冪等性: Confirm 既に処理済み。OrderId={OrderId}", orderId);
            return Math.Abs(existingTx.Points);
        }

        var reserveTx = await transactionRepository.FindByReferenceAsync(orderId, TransactionTypes.Reserve, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var account = await accountRepository.FindByUserIdAsync(userId, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var confirmedPoints = Math.Abs(reserveTx.Points);
        account.ConfirmReservation(confirmedPoints);

        var userIdGuid = Guid.Parse(userId);
        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Type = TransactionTypes.Redeem,
            Points = -confirmedPoints,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = orderId,
            ReferenceType = ReferenceTypes.Order,
            Description = $"注文 {orderId} のポイント消費確定"
        }, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            AggregateType = AggregateTypes.PointAccount,
            AggregateId = account.Id,
            EventType = EventTopics.PointRedeemed,
            Payload = JsonSerializer.Serialize(new PointsRedeemedEvent(
                userId, confirmedPoints, orderId,
                account.AvailablePoints, "", now))
        }, ct);

        try
        {
            await accountRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: UserId={UserId}", userId);
            throw new Exceptions.ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。", ex);
        }

        await cacheService.InvalidateBalanceCacheAsync(userId, ct);
        return confirmedPoints;
    }

    public async Task<int> ReleasePointsAsync(
        string userId, string orderId, CancellationToken ct = default)
    {
        var existingTx = await transactionRepository.FindByReferenceAsync(orderId, TransactionTypes.Release, ct);
        if (existingTx is not null)
        {
            logger.LogInformation("冪等性: Release 既に処理済み。OrderId={OrderId}", orderId);
            return existingTx.Points;
        }

        var reserveTx = await transactionRepository.FindByReferenceAsync(orderId, TransactionTypes.Reserve, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var account = await accountRepository.FindByUserIdAsync(userId, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var releasedPoints = Math.Abs(reserveTx.Points);
        account.ReleaseReservation(releasedPoints);

        var userIdGuid = Guid.Parse(userId);
        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Type = TransactionTypes.Release,
            Points = releasedPoints,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = orderId,
            ReferenceType = ReferenceTypes.Order,
            Description = $"注文 {orderId} のポイント仮消費解放"
        }, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            AggregateType = AggregateTypes.PointAccount,
            AggregateId = account.Id,
            EventType = EventTopics.PointReleased,
            Payload = JsonSerializer.Serialize(new PointsReleasedEvent(
                userId, releasedPoints, orderId, "", now))
        }, ct);

        try
        {
            await accountRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: UserId={UserId}", userId);
            throw new Exceptions.ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。", ex);
        }

        await cacheService.InvalidateBalanceCacheAsync(userId, ct);
        return releasedPoints;
    }

    public async Task<PointBalanceResponse> GetBalanceAsync(
        string userId, CancellationToken ct = default)
    {
        var cached = await cacheService.GetBalanceCacheAsync(userId, ct);
        if (cached is not null) return cached;

        var account = await accountRepository.FindByUserIdAsync(userId, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var response = new PointBalanceResponse(
            account.UserId.ToString(), account.AvailablePoints, account.PendingPoints,
            account.TotalEarned, account.TotalSpent, account.TotalExpired);

        await cacheService.SetBalanceCacheAsync(userId, response, ct);
        return response;
    }

    public async Task<PagedResult<PointTransactionResponse>> GetTransactionHistoryAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, totalCount) = await transactionRepository.GetPagedAsync(
            userId, page, pageSize, ct);

        var responses = items.Select(t => new PointTransactionResponse(
            t.Id, t.Type, t.Points, t.BalanceAfter,
            t.ReferenceId, t.ReferenceType,
            t.Description, t.ExpiresAt, t.CreatedAt)).ToList();

        return new PagedResult<PointTransactionResponse>(responses, totalCount, page, pageSize);
    }

    public async Task AdjustPointsAsync(
        string userId, AdjustPointsRequest request,
        string adminUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default)
    {
        var account = await accountRepository.FindByUserIdAsync(userId, ct)
            ?? throw new PointAccountNotFoundException(userId);

        var pointsBefore = account.AvailablePoints;
        account.AdjustPoints(request.Points);

        var userIdGuid = Guid.Parse(userId);
        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = userIdGuid,
            Type = TransactionTypes.Adjust,
            Points = request.Points,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = request.ReferenceId,
            ReferenceType = ReferenceTypes.AdminAdjust,
            Description = request.Reason
        }, ct);

        await auditLogRepository.AddAsync(new PointAuditLog
        {
            AdminUserId = adminUserId,
            TargetUserId = userId,
            Action = request.Points >= 0 ? AuditActions.Add : AuditActions.Subtract,
            PointsBefore = pointsBefore,
            PointsAfter = account.AvailablePoints,
            PointsChanged = request.Points,
            Reason = request.Reason,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, ct);

        try
        {
            await accountRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: UserId={UserId}", userId);
            throw new Exceptions.ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。", ex);
        }

        logger.LogInformation(
            "ポイント調整: AdminUserId={AdminUserId}, TargetUserId={TargetUserId}, " +
            "Points={Points}, Before={Before}, After={After}",
            adminUserId, userId, request.Points, pointsBefore, account.AvailablePoints);

        await cacheService.InvalidateBalanceCacheAsync(userId, ct);
    }

    public async Task<ExpiringPointsResponse> GetExpiringPointsAsync(
        string userId, CancellationToken ct = default)
    {
        var expiries = await expiryRepository.FindExpiringWithinAsync(userId, 30, ct);
        var items = expiries.Select(e => new ExpiringPointItem(
            e.Points, e.ExpiresAt, $"ポイント付与 ({e.SourceTransactionId ?? "N/A"})")).ToList();
        var totalExpiring = items.Sum(i => i.Points);
        return new ExpiringPointsResponse(items, totalExpiring);
    }

    public async Task CreateAccountAsync(string userId, CancellationToken ct = default)
    {
        var existing = await accountRepository.FindByUserIdAsync(userId, ct);
        if (existing is not null)
        {
            logger.LogInformation("ポイントアカウント既存: UserId={UserId}", userId);
            return;
        }

        await accountRepository.AddAsync(new PointAccount { UserId = Guid.Parse(userId) }, ct);
        await accountRepository.SaveChangesAsync(ct);
        logger.LogInformation("ポイントアカウント作成: UserId={UserId}", userId);
    }

    public async Task AnonymizeUserDataAsync(string userId, CancellationToken ct = default)
    {
        var anonymizedId = $"anon-{Guid.NewGuid():N}"[..36];

        await using var transaction = await accountRepository.BeginTransactionAsync(ct);
        try
        {
            await accountRepository.AnonymizeAccountAsync(userId, anonymizedId, ct);
            await transactionRepository.AnonymizeTransactionsAsync(userId, anonymizedId, ct);
            await expiryRepository.AnonymizeExpiriesAsync(userId, anonymizedId, ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        await cacheService.InvalidateBalanceCacheAsync(userId, ct);
        logger.LogInformation("ユーザーデータ匿名化完了: 元UserId={UserId}", userId);
    }

    private async Task ConsumePointsFifoAsync(
        PointAccount account, int pointsToConsume, CancellationToken ct)
    {
        var activeExpiries = await expiryRepository.FindActiveByAccountIdAsync(account.Id, ct);
        var remaining = pointsToConsume;

        foreach (var expiry in activeExpiries)
        {
            if (remaining <= 0) break;

            if (expiry.Points <= remaining)
            {
                remaining -= expiry.Points;
                expiry.Status = ExpiryStatuses.Consumed;
            }
            else
            {
                expiry.Points -= remaining;
                remaining = 0;
            }
        }
    }
}
