using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="StockOutRequest"/> のバリデーター。
/// 出庫リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 商品 ID は必須、出庫数量は 1 以上、出庫理由は必須・最大 100 文字。
/// </remarks>
public class StockOutRequestValidator : AbstractValidator<StockOutRequest>
{
    public StockOutRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1)
            .WithMessage("出庫数量は 1 以上を指定してください");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
    }
}
