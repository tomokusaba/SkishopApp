using FluentValidation;
using SalesManagementService.DTOs.Requests;

namespace SalesManagementService.Infrastructure.Validators;

public class OrderCreateRequestValidator : AbstractValidator<OrderCreateRequest>
{
    public OrderCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("注文には1つ以上の商品が必要です")
            .Must(items => items.Count <= 50)
            .WithMessage("注文商品は50件以内で指定してください");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Sku).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 99);
        });

        RuleFor(x => x.ShippingAddress).NotNull().WithMessage("配送先住所は必須です");
        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress.RecipientName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ShippingAddress.PostalCode).NotEmpty().MaximumLength(10);
            RuleFor(x => x.ShippingAddress.Prefecture).NotEmpty().MaximumLength(50);
            RuleFor(x => x.ShippingAddress.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ShippingAddress.AddressLine1).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ShippingAddress.PhoneNumber).NotEmpty().MaximumLength(20);
        });

        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.UsedPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
