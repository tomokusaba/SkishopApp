using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("商品 ID は必須です").WithErrorCode("CART-V001")
            .MaximumLength(100).WithErrorCode("CART-V002");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("商品名は必須です").WithErrorCode("CART-V003")
            .MaximumLength(200).WithErrorCode("CART-V004");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU は必須です").WithErrorCode("CART-V005")
            .MaximumLength(100).WithErrorCode("CART-V006");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("単価は 0 より大きい値を指定してください").WithErrorCode("CART-V007")
            .LessThanOrEqualTo(99999999.99m).WithErrorCode("CART-V008");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("数量は 1 以上を指定してください").WithErrorCode("CART-V009")
            .LessThanOrEqualTo(10).WithMessage("数量は 10 以下を指定してください").WithErrorCode("CART-V010");
    }
}
