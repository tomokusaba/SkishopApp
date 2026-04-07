namespace Frontend.DTOs;

public record ValidateCouponRequest(string CouponCode, decimal CartTotal);

public record CouponValidationResult(
    bool IsValid,
    string? ErrorMessage,
    decimal DiscountAmount,
    string? CouponCode,
    string? Description);

public record CouponDto(
    string Id,
    string Code,
    string Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    DateTime? ExpiresAt,
    bool IsUsed);
