namespace CouponService.DTOs.Responses;

public record CouponResponse(
    string Id,
    string Code,
    string? CouponTypeId,
    string? CampaignId,
    int DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int MaxUsageCount,
    int CurrentUsageCount,
    int MaxUsagePerUser,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CouponSummaryResponse(
    string Id,
    string Code,
    int DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    bool IsActive);

public record CampaignResponse(
    string Id,
    string Name,
    string? Description,
    int Status,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int MaxCoupons,
    int IssuedCount,
    DateTimeOffset CreatedAt);

public record UserCouponResponse(
    string Id,
    string CouponCode,
    int DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    string Status,
    DateTimeOffset AcquiredAt);

public record ValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    decimal DiscountAmount,
    string? CouponId = null,
    int? DiscountType = null,
    decimal? DiscountValue = null);

public record CouponUsageResponse(
    string Id,
    string UserId,
    string OrderId,
    decimal DiscountAmount,
    DateTimeOffset UsedAt);

public record CouponAnalyticsResponse(
    string CouponId,
    string Code,
    int TotalUsageCount,
    decimal TotalDiscountAmount,
    int UniqueUserCount);

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CalculateDiscountResponse(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    string? CouponId,
    decimal DiscountAmount,
    int DiscountType,
    decimal DiscountValue);

public record FraudCheckResult(bool IsSuspicious, string? Reason)
{
    public static FraudCheckResult Clear => new(false, null);
    public static FraudCheckResult Suspicious(string reason) => new(true, reason);
}
