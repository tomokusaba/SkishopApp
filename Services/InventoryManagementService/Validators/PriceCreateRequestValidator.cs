using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="PriceCreateRequest"/> のバリデーター。
/// 価格新規作成リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール:
/// <list type="bullet">
///   <item>商品 ID は必須</item>
///   <item>通常価格は 0 以上</item>
///   <item>セール価格は通常価格より低いこと（指定時のみ）</item>
///   <item>セール終了日はセール開始日より後であること（両方指定時のみ）</item>
///   <item>通貨コードは JPY, USD, EUR のいずれか</item>
/// </list>
/// </remarks>
public class PriceCreateRequestValidator : AbstractValidator<PriceCreateRequest>
{
    public PriceCreateRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.RegularPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).LessThan(x => x.RegularPrice)
            .When(x => x.SalePrice.HasValue)
            .WithMessage("セール価格は通常価格より低く設定してください");
        RuleFor(x => x.SaleEndDate).GreaterThan(x => x.SaleStartDate)
            .When(x => x.SaleStartDate.HasValue && x.SaleEndDate.HasValue)
            .WithMessage("セール終了日はセール開始日より後に設定してください");
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3)
            .Matches("^(JPY|USD|EUR)$")
            .WithMessage("通貨コードは JPY, USD, EUR のいずれかを指定してください");
    }
}
