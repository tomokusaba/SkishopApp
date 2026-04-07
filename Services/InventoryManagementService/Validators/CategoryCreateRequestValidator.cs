using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="CategoryCreateRequest"/> のバリデーター。
/// カテゴリ新規作成リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: カテゴリ名は必須・最大 255 文字、説明は最大 2000 文字。
/// </remarks>
public class CategoryCreateRequestValidator : AbstractValidator<CategoryCreateRequest>
{
    public CategoryCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}
