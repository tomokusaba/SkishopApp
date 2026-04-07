using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;

namespace CouponService.Services.Interfaces;

public interface ICouponService
{
    Task<PagedResponse<CouponSummaryResponse>> GetAvailableCouponsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CouponResponse?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<CouponResponse> CreateCouponAsync(CreateCouponRequest request, CancellationToken ct = default);
    Task<CouponResponse> UpdateCouponAsync(string id, UpdateCouponRequest request, CancellationToken ct = default);
    Task DeactivateCouponAsync(string id, CancellationToken ct = default);
    Task<UserCouponResponse> AcquireCouponAsync(string code, string userId, CancellationToken ct = default);
    Task<List<UserCouponResponse>> GetUserCouponsAsync(string userId, CancellationToken ct = default);
    Task<ValidationResult> ValidateAndApplyAsync(string couponCode, string userId, decimal orderAmount, string? categoryId, CancellationToken ct = default);
    Task<CouponUsageResponse> RedeemCouponAsync(string userId, RedeemCouponRequest request, CancellationToken ct = default);
    Task ReleaseCouponAsync(ReleaseCouponRequest request, CancellationToken ct = default);
    Task<CalculateDiscountResponse> CalculateDiscountAsync(CalculateDiscountRequest request, CancellationToken ct = default);
    Task<PagedResponse<CouponResponse>> GetAllCouponsAsync(CouponQueryParams query, CancellationToken ct = default);
    Task<PagedResponse<CouponUsageResponse>> GetCouponUsagesAsync(string couponId, int page, int pageSize, CancellationToken ct = default);
    Task<int> DeactivateExpiredCouponsAsync(CancellationToken ct = default);
}

public interface ICampaignService
{
    Task<CampaignResponse> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken ct = default);
    Task<CampaignResponse?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PagedResponse<CampaignResponse>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CampaignResponse> UpdateCampaignAsync(string id, UpdateCampaignRequest request, CancellationToken ct = default);
    Task ActivateCampaignAsync(string id, CancellationToken ct = default);
    Task PauseCampaignAsync(string id, CancellationToken ct = default);
    Task<int> CompleteExpiredCampaignsAsync(CancellationToken ct = default);
}

public interface ICouponRuleEngine
{
    Task<ValidationResult> ValidateAsync(string couponCode, string userId, decimal orderAmount, string? categoryId, CancellationToken ct = default);
    Task<ValidationResult> EvaluateRulesAsync(Models.Coupon coupon, DTOs.Requests.ValidateCouponRequest request, List<Models.CouponRestriction> restrictions, CancellationToken ct = default);
}

public interface IFraudDetectionService
{
    Task<FraudCheckResult> CheckAsync(string userId, Models.Coupon coupon, CancellationToken ct = default);
    Task<bool> IsSuspiciousAsync(string userId, CancellationToken ct = default);
    Task<bool> CheckFraudAsync(string userId, string couponCode, decimal orderAmount, CancellationToken ct = default);
}

public interface ICouponAnalyticsService
{
    Task<CouponAnalyticsResponse> GetAnalyticsAsync(string couponId, CancellationToken ct = default);
    Task<CouponAnalyticsResponse> GetOverallAnalyticsAsync(CancellationToken ct = default);
    Task<double> GetRedemptionRateAsync(string? campaignId = null, CancellationToken ct = default);
    Task<List<CouponResponse>> GetTopCouponsAsync(int topN = 10, CancellationToken ct = default);
    Task<List<Models.CouponUsage>> GetUsagesByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public interface ICouponCacheService
{
    Task<Models.Coupon?> GetCouponAsync(string code, CancellationToken ct = default);
    Task SetCouponAsync(string code, Models.Coupon coupon, CancellationToken ct = default);
    Task InvalidateAsync(string code, CancellationToken ct = default);
    Task<int?> GetUserUsageCountAsync(string couponId, string userId, CancellationToken ct = default);
    Task SetUserUsageCountAsync(string couponId, string userId, int count, CancellationToken ct = default);
}

public interface ICouponCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
    Task<List<string>> GenerateBatchAsync(int count, CancellationToken ct = default);
}
