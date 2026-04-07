using System.ComponentModel.DataAnnotations;

namespace CouponService.DTOs.Requests;

public record CreateCouponRequest(
    [Required, StringLength(30, MinimumLength = 3), RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "クーポンコードは大文字英数字とハイフンのみ使用可能です")]
    string Code,
    string? CouponTypeId,
    string? CampaignId,
    [Required] int DiscountType,
    [Required, Range(0.01, 1000000)] decimal DiscountValue,
    decimal? MaxDiscountAmount,
    [Range(0, 1000000)] decimal MinOrderAmount,
    [Range(1, 1000000)] int MaxUsageCount,
    [Range(1, 1000)] int MaxUsagePerUser,
    [Required] DateTimeOffset ValidFrom,
    [Required] DateTimeOffset ValidUntil);

public record UpdateCouponRequest(
    string? CouponTypeId,
    string? CampaignId,
    int? DiscountType,
    [Range(0.01, 1000000)] decimal? DiscountValue,
    decimal? MaxDiscountAmount,
    [Range(0, 1000000)] decimal? MinOrderAmount,
    [Range(1, 1000000)] int? MaxUsageCount,
    [Range(1, 1000)] int? MaxUsagePerUser,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    bool? IsActive);

public record ValidateCouponRequest(
    [Required, StringLength(30)] string Code,
    [Required, Range(0.01, 100000000)] decimal OrderAmount,
    string? CategoryId);

public record RedeemCouponRequest(
    [Required, StringLength(36)] string CouponId,
    [Required, StringLength(36)] string OrderId,
    [Required, Range(0.01, 100000000)] decimal DiscountAmount,
    [Required, StringLength(36)] string UserId);

public record ReleaseCouponRequest(
    [Required, StringLength(36)] string CouponId,
    [Required, StringLength(36)] string OrderId);

public record CreateCampaignRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Description,
    [Required] DateTimeOffset StartDate,
    [Required] DateTimeOffset EndDate,
    [Range(1, 1000000)] int MaxCoupons);

public record UpdateCampaignRequest(
    [StringLength(200, MinimumLength = 1)] string? Name,
    [StringLength(2000)] string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    [Range(1, 1000000)] int? MaxCoupons);

public record CouponQueryParams(
    int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    string? SortBy = "CreatedAt",
    bool Descending = true);

public record CalculateDiscountRequest(
    [Required, StringLength(50)] string CouponCode,
    [Required, StringLength(36)] string UserId,
    [Required, Range(0.01, 100000000)] decimal OrderAmount,
    string? CategoryId);
