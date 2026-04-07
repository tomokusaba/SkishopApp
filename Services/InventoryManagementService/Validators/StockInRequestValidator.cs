using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="StockInRequest"/> のバリデーター。
/// 入庫リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 商品 ID は必須、入庫数量は 1 以上。
/// </remarks>
public class StockInRequestValidator : AbstractValidator<StockInRequest>
{
    public StockInRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1)
            .WithMessage("入庫数量は 1 以上を指定してください");
    }
}
