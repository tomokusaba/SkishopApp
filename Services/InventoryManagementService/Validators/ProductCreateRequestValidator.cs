using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="ProductCreateRequest"/> のバリデーター。
/// 商品新規登録リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール:
/// <list type="bullet">
///   <item>SKU は必須、最大 100 文字、'XX-YYYY-NNN' 形式（大文字英字 2〜5 文字 + ハイフン + 英数字 + ハイフン + 数字 3 桁以上）</item>
///   <item>商品名は必須、最大 255 文字</item>
///   <item>カテゴリ ID は必須</item>
///   <item>重量は正の数（指定時のみ）</item>
///   <item>説明は最大 5000 文字、ブランドは最大 100 文字</item>
/// </list>
/// </remarks>
public class ProductCreateRequestValidator : AbstractValidator<ProductCreateRequest>
{
    public ProductCreateRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100)
            .Matches(@"^[A-Z]{2,5}-[A-Z0-9]+-\d{3,}$")
            .WithMessage("SKU は 'XX-YYYY-NNN' 形式で入力してください");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Weight).GreaterThan(0).When(x => x.Weight.HasValue);
        RuleFor(x => x.Description).MaximumLength(5000).When(x => x.Description is not null);
        RuleFor(x => x.Brand).MaximumLength(100).When(x => x.Brand is not null);
    }
}
