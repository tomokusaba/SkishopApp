using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    private static readonly string[] ValidPaymentMethods =
        ["CREDIT_CARD", "CONVENIENCE_STORE", "BANK_TRANSFER"];

    public CheckoutRequestValidator()
    {
        RuleFor(x => x.CartId)
            .NotEmpty().WithMessage("カート ID は必須です").WithErrorCode("PAY-V001")
            .MaximumLength(36).WithErrorCode("PAY-V002");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("決済方法は必須です").WithErrorCode("PAY-V003")
            .Must(m => ValidPaymentMethods.Contains(m))
            .WithMessage("サポートされていない決済方法です").WithErrorCode("PAY-V004");

        RuleFor(x => x.UsedPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UsedPoints.HasValue)
            .WithMessage("使用ポイントは 0 以上を指定してください").WithErrorCode("PAY-V005");

        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress!.RecipientName)
                .NotEmpty().WithMessage("受取人名は必須です").WithErrorCode("PAY-V010")
                .MaximumLength(100).WithErrorCode("PAY-V011");

            RuleFor(x => x.ShippingAddress!.PostalCode)
                .NotEmpty().WithMessage("郵便番号は必須です").WithErrorCode("PAY-V012")
                .Matches(@"^\d{3}-?\d{4}$").WithMessage("郵便番号の形式が不正です").WithErrorCode("PAY-V013");

            RuleFor(x => x.ShippingAddress!.PhoneNumber)
                .NotEmpty().WithMessage("電話番号は必須です").WithErrorCode("PAY-V014")
                .Matches(@"^[\d\-]+$").WithMessage("電話番号の形式が不正です").WithErrorCode("PAY-V015");
        });
    }
}
