namespace SalesManagementService.Infrastructure.ExternalServices;

public class DevelopmentInventoryClient : IInventoryClient
{
    public Task<ReserveInventoryResponse> ReserveInventoryAsync(
        IReadOnlyList<CartItemDto> items, int deadlineMs, CancellationToken ct = default)
        => Task.FromResult(new ReserveInventoryResponse(Guid.NewGuid().ToString()));

    public Task ReleaseReservationAsync(string reservationId, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RestoreInventoryAsync(string productId, int quantity, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class DevelopmentCartClient : ICartClient
{
    public Task<GetCartResponse> GetCartAsync(string userId, int deadlineMs, CancellationToken ct = default)
        => Task.FromResult(new GetCartResponse(Array.Empty<CartItemDto>()));

    public Task ClearCartAsync(string userId, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class DevelopmentCouponClient : ICouponClient
{
    public Task<ValidateCouponResponse> ValidateCouponAsync(
        string couponCode, string userId, int deadlineMs, CancellationToken ct = default)
        => Task.FromResult(new ValidateCouponResponse(0m, string.Empty));

    public Task ReleaseCouponAsync(string? orderId, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class DevelopmentPointClient : IPointClient
{
    public Task<ReservePointsResponse> ReservePointsAsync(
        string userId, int points, int deadlineMs, CancellationToken ct = default)
        => Task.FromResult(new ReservePointsResponse(Guid.NewGuid().ToString()));

    public Task ReleasePointsAsync(string reservationId, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AwardPointsAsync(string userId, decimal orderAmount, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AdjustPointsForReturnAsync(
        string userId, decimal refundAmount, int usedPoints, int deadlineMs, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class DevelopmentPaymentClient : IPaymentClient
{
    public Task<PaymentResult> ProcessPaymentAsync(
        decimal amount, string paymentMethod, CancellationToken ct = default)
        => Task.FromResult(new PaymentResult(Guid.NewGuid().ToString(), "CAPTURED"));

    public Task RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default)
        => Task.CompletedTask;
}
