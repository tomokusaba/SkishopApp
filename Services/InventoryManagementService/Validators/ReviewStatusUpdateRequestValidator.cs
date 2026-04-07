using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="ReviewStatusUpdateRequest"/> のバリデーター。
/// レビューステータス更新リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: ステータスは必須、"APPROVED" または "REJECTED" のいずれかのみ許可。
/// </remarks>
public class ReviewStatusUpdateRequestValidator : AbstractValidator<ReviewStatusUpdateRequest>
{
    public ReviewStatusUpdateRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("ステータスは必須です")
            .Matches("^(APPROVED|REJECTED)$").WithMessage("ステータスは APPROVED または REJECTED を指定してください");
    }
}
