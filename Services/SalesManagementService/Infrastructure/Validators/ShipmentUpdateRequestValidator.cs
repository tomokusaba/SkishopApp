using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class ShipmentUpdateRequestValidator : AbstractValidator<ShipmentUpdateRequest>
{
    private static readonly string[] AllowedStatuses =
        ["PREPARING", "SHIPPED", "IN_TRANSIT", "DELIVERED", "FAILED"];

    public ShipmentUpdateRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("出荷ステータスは PREPARING, SHIPPED, IN_TRANSIT, DELIVERED, FAILED のいずれかである必要があります");
        RuleFor(x => x.TrackingNumber).MaximumLength(100);
    }
}
