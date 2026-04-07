using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="ReviewResponseCreateRequest"/> のバリデーター。
/// レビュー返信作成リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 返信内容は必須、最大 5000 文字。
/// </remarks>
public class ReviewResponseCreateRequestValidator : AbstractValidator<ReviewResponseCreateRequest>
{
    public ReviewResponseCreateRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("返信内容は必須です")
            .MaximumLength(5000).WithMessage("返信内容は5000文字以内で入力してください");
    }
}
