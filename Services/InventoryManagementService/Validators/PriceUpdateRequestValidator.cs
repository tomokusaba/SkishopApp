using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="PriceUpdateRequest"/> のバリデーター。
/// 価格更新リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 各フィールドは指定時のみ検証（部分更新対応）。
/// 通常価格・セール価格は 0 以上、セール終了日は開始日より後。
/// </remarks>
public class PriceUpdateRequestValidator : AbstractValidator<PriceUpdateRequest>
{
    public PriceUpdateRequestValidator()
    {
        RuleFor(x => x.RegularPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.RegularPrice.HasValue);

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SalePrice.HasValue);

        RuleFor(x => x.SaleEndDate)
            .GreaterThan(x => x.SaleStartDate)
            .When(x => x.SaleStartDate.HasValue && x.SaleEndDate.HasValue);
    }
}
