using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class ReturnCreateRequestValidator : AbstractValidator<ReturnCreateRequest>
{
    private static readonly string[] AllowedReasons =
        ["DEFECTIVE", "WRONG_ITEM", "SIZE_MISMATCH", "NOT_AS_DESCRIBED", "CHANGED_MIND", "OTHER"];

    public ReturnCreateRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderItemId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(r => AllowedReasons.Contains(r))
            .WithMessage("返品理由は DEFECTIVE, WRONG_ITEM, SIZE_MISMATCH, NOT_AS_DESCRIBED, CHANGED_MIND, OTHER のいずれかである必要があります");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 99);
        RuleFor(x => x.ReasonDetail).MaximumLength(2000);
    }
}
