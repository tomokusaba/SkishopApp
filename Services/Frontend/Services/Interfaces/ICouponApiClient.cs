using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// クーポン API クライアントインターフェース
/// </summary>
public interface ICouponApiClient
{
    Task<CouponValidationResult?> ValidateCouponAsync(string couponCode, decimal cartTotal, CancellationToken ct = default);
    Task<List<CouponDto>> GetAvailableCouponsAsync(CancellationToken ct = default);
    Task<List<CouponDto>> GetMyCouponsAsync(CancellationToken ct = default);
    Task<CouponDto?> AcquireCouponAsync(string couponId, CancellationToken ct = default);
}
