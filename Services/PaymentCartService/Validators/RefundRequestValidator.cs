using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("返金金額は 0 より大きい値を指定してください").WithErrorCode("REFUND-V001");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("返金理由は必須です").WithErrorCode("REFUND-V002")
            .MaximumLength(500).WithErrorCode("REFUND-V003");
    }
}
