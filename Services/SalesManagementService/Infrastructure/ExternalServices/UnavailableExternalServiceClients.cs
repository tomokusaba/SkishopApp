namespace SalesManagementService.Infrastructure.ExternalServices;

public class UnavailableInventoryClient : IInventoryClient
{
    public Task<ReserveInventoryResponse> ReserveInventoryAsync(
        IReadOnlyList<CartItemDto> items,
        int deadlineMs,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Inventory client is not configured for this environment.");

    public Task ReleaseReservationAsync(string reservationId, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Inventory client is not configured for this environment.");

    public Task RestoreInventoryAsync(string productId, int quantity, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Inventory client is not configured for this environment.");
}

public class UnavailableCartClient : ICartClient
{
    public Task<GetCartResponse> GetCartAsync(string userId, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Cart client is not configured for this environment.");

    public Task ClearCartAsync(string userId, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Cart client is not configured for this environment.");
}

public class UnavailableCouponClient : ICouponClient
{
    public Task<ValidateCouponResponse> ValidateCouponAsync(
        string couponCode,
        string userId,
        int deadlineMs,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Coupon client is not configured for this environment.");

    public Task ReleaseCouponAsync(string? orderId, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Coupon client is not configured for this environment.");
}

public class UnavailablePointClient : IPointClient
{
    public Task<ReservePointsResponse> ReservePointsAsync(
        string userId,
        int points,
        int deadlineMs,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Point client is not configured for this environment.");

    public Task ReleasePointsAsync(string reservationId, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Point client is not configured for this environment.");

    public Task AwardPointsAsync(string userId, decimal orderAmount, int deadlineMs, CancellationToken ct = default)
        => throw new InvalidOperationException("Point client is not configured for this environment.");

    public Task AdjustPointsForReturnAsync(
        string userId,
        decimal refundAmount,
        int usedPoints,
        int deadlineMs,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Point client is not configured for this environment.");
}

public class UnavailablePaymentClient : IPaymentClient
{
    public Task<PaymentResult> ProcessPaymentAsync(
        decimal amount,
        string paymentMethod,
        CancellationToken ct = default)
        => throw new InvalidOperationException("Payment client is not configured for this environment.");

    public Task RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default)
        => throw new InvalidOperationException("Payment client is not configured for this environment.");
}
