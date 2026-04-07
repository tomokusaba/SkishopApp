using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class OrderStatusUpdateRequestValidator : AbstractValidator<OrderStatusUpdateRequest>
{
    private static readonly HashSet<string> ValidStatuses =
    [
        "PENDING", "CONFIRMED", "PROCESSING", "SHIPPED", "DELIVERED",
        "RETURNED", "REFUNDED", "CANCELLED", "INVENTORY_SHORTAGE",
        "PAYMENT_FAILED", "PENDING_PAYMENT"
    ];

    public OrderStatusUpdateRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("ステータスは必須です")
            .MaximumLength(20)
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("無効な注文ステータスです");
    }
}
