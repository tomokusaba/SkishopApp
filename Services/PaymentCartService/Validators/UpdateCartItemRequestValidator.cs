using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("数量は 1 以上を指定してください").WithErrorCode("UPDATE-V001")
            .LessThanOrEqualTo(10).WithMessage("数量は 10 以下を指定してください").WithErrorCode("UPDATE-V002");
    }
}
