using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class ReturnProcessRequestValidator : AbstractValidator<ReturnProcessRequest>
{
    private static readonly HashSet<string> ValidStatuses =
    [
        "APPROVED", "REJECTED", "RECEIVED", "REFUNDED"
    ];

    public ReturnProcessRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("ステータスは必須です")
            .MaximumLength(20)
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("無効な返品ステータスです");

        RuleFor(x => x.AdminNotes)
            .MaximumLength(2000).WithMessage("管理者メモは2000文字以内で入力してください");
    }
}
