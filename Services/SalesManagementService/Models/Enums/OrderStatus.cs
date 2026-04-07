namespace SalesManagementService.Models.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Returned,
    Refunded,
    Cancelled,
    InventoryShortage,
    PaymentFailed,
    PendingPayment
}
