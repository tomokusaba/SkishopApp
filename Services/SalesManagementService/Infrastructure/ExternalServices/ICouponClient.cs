namespace SalesManagementService.Infrastructure.ExternalServices;

public record ValidateCouponResponse(decimal DiscountAmount, string CouponId);

public interface ICouponClient
{
    Task<ValidateCouponResponse> ValidateCouponAsync(
        string couponCode, string userId, int deadlineMs, CancellationToken ct = default);

    Task ReleaseCouponAsync(string? orderId, int deadlineMs, CancellationToken ct = default);
}
