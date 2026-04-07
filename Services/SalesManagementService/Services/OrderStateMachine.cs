using SalesManagementService.Infrastructure.Exceptions;

namespace SalesManagementService.Services;

public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<Models.Enums.OrderStatus, string> StatusMap =
        new Dictionary<Models.Enums.OrderStatus, string>
        {
            [Models.Enums.OrderStatus.Pending] = "PENDING",
            [Models.Enums.OrderStatus.PendingPayment] = "PENDING_PAYMENT",
            [Models.Enums.OrderStatus.Confirmed] = "CONFIRMED",
            [Models.Enums.OrderStatus.Processing] = "PROCESSING",
            [Models.Enums.OrderStatus.Shipped] = "SHIPPED",
            [Models.Enums.OrderStatus.Delivered] = "DELIVERED",
            [Models.Enums.OrderStatus.Returned] = "RETURNED",
            [Models.Enums.OrderStatus.Refunded] = "REFUNDED",
            [Models.Enums.OrderStatus.Cancelled] = "CANCELLED",
            [Models.Enums.OrderStatus.InventoryShortage] = "INVENTORY_SHORTAGE",
            [Models.Enums.OrderStatus.PaymentFailed] = "PAYMENT_FAILED"
        };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        ["PENDING"] = ["CONFIRMED", "CANCELLED", "PENDING_PAYMENT", "PAYMENT_FAILED"],
        ["PENDING_PAYMENT"] = ["CONFIRMED", "CANCELLED", "PAYMENT_FAILED"],
        ["CONFIRMED"] = ["PROCESSING", "CANCELLED"],
        ["PROCESSING"] = ["SHIPPED", "CANCELLED", "INVENTORY_SHORTAGE"],
        ["SHIPPED"] = ["DELIVERED"],
        ["DELIVERED"] = ["RETURNED"],
        ["RETURNED"] = ["REFUNDED"],
        ["INVENTORY_SHORTAGE"] = ["PROCESSING", "CANCELLED"],
        ["PAYMENT_FAILED"] = ["PENDING_PAYMENT", "CANCELLED"],
        ["REFUNDED"] = [],
        ["CANCELLED"] = []
    };

    public static bool CanTransition(string currentStatus, string newStatus)
    {
        if (AllowedTransitions.TryGetValue(currentStatus, out var allowed))
            return allowed.Contains(newStatus);
        return false;
    }

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!CanTransition(currentStatus, newStatus))
            throw new InvalidOrderStateException(
                $"注文ステータスを '{currentStatus}' から '{newStatus}' に変更できません");
    }

    public static void TransitionTo(Models.Order order, Models.Enums.OrderStatus newStatus)
    {
        var newStatusStr = StatusMap[newStatus];
        ValidateTransition(order.Status, newStatusStr);
        order.Status = newStatusStr;
    }
}
