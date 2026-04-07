using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class ShipmentCreateRequestValidator : AbstractValidator<ShipmentCreateRequest>
{
    public ShipmentCreateRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TrackingNumber).MaximumLength(100);
    }
}
