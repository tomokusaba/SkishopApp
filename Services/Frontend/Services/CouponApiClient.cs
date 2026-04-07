using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class CouponApiClient(IApiGatewayClient apiClient, ILogger<CouponApiClient> logger) : ICouponApiClient
{
    public async Task<CouponValidationResult?> ValidateCouponAsync(string couponCode, decimal cartTotal, CancellationToken ct = default)
    {
        logger.LogInformation("クーポン検証: CouponCode={CouponCode}", couponCode);
        return await apiClient.PostAsync<ValidateCouponRequest, CouponValidationResult>(
            "/api/v1/coupons/validate", new ValidateCouponRequest(couponCode, cartTotal), ct);
    }

    public async Task<List<CouponDto>> GetAvailableCouponsAsync(CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<List<CouponDto>>("/api/v1/coupons/available", ct);
        return result ?? [];
    }

    public async Task<List<CouponDto>> GetMyCouponsAsync(CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<List<CouponDto>>("/api/v1/coupons/my", ct);
        return result ?? [];
    }

    public async Task<CouponDto?> AcquireCouponAsync(string couponId, CancellationToken ct = default)
    {
        logger.LogInformation("クーポン取得: CouponId={CouponId}", couponId);
        return await apiClient.PostAsync<object, CouponDto>(
            "/api/v1/coupons/acquire", new { CouponId = couponId }, ct);
    }
}

