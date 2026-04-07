using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="ProductUpdateRequest"/> のバリデーター。
/// 商品更新リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 各フィールドは指定時のみ検証（部分更新対応）。
/// 商品名は最大 255 文字、説明は最大 5000 文字、ブランドは最大 100 文字、重量は 0 以上。
/// </remarks>
public class ProductUpdateRequestValidator : AbstractValidator<ProductUpdateRequest>
{
    public ProductUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(255)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Brand)
            .MaximumLength(100)
            .When(x => x.Brand is not null);

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Weight.HasValue);
    }
}
