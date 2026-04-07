using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class OrderCancelRequestValidator : AbstractValidator<OrderCancelRequest>
{
    public OrderCancelRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("キャンセル理由は必須です")
            .MaximumLength(500).WithMessage("キャンセル理由は500文字以内で入力してください");
    }
}
