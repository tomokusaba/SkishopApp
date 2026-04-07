using SalesManagementService.Infrastructure.ExternalServices;

namespace SalesManagementService.Infrastructure.Saga;

public class SagaContext
{
    public string UserId { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public IReadOnlyList<CartItemDto>? CartItems { get; set; }
    public string? ReservationId { get; set; }
    public decimal CouponDiscount { get; set; }
    public string? CouponId { get; set; }
    public string? PointReservationId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
}
