namespace CouponService.Consumers;

public record CouponAppliedEvent(
    string CouponId, string CouponCode, string UserId, string OrderId,
    decimal DiscountApplied, DateTimeOffset OccurredAt);

public record CouponReleasedEvent(
    string CouponId, string CouponCode, string UserId, string OrderId,
    decimal ReleasedAmount, DateTimeOffset OccurredAt);

public record CouponExpiredEvent(
    string CouponId, string Code, DateTimeOffset OccurredAt);

public record OrderCancelledEvent(
    string OrderId, string? CouponId, string UserId);

public record UserDeletedEvent(string UserId, DateTimeOffset OccurredAt);
