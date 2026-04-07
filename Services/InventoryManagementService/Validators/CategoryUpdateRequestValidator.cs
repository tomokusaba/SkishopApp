using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="CategoryUpdateRequest"/> のバリデーター。
/// カテゴリ更新リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 指定された場合のみ検証を行う（部分更新対応）。
/// カテゴリ名は最大 255 文字、説明は最大 2000 文字。
/// </remarks>
public class CategoryUpdateRequestValidator : AbstractValidator<CategoryUpdateRequest>
{
    public CategoryUpdateRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(255).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}
