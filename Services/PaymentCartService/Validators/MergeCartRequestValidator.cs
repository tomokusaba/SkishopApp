using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class MergeCartRequestValidator : AbstractValidator<MergeCartRequest>
{
    public MergeCartRequestValidator()
    {
        RuleFor(x => x.GuestCartId)
            .NotEmpty().WithMessage("ゲストカート ID は必須です").WithErrorCode("MERGE-V001")
            .MaximumLength(36).WithErrorCode("MERGE-V002");
    }
}
