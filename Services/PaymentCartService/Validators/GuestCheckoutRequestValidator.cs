using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class GuestCheckoutRequestValidator : AbstractValidator<GuestCheckoutRequest>
{
    private static readonly string[] ValidPaymentMethods =
        ["CREDIT_CARD", "CONVENIENCE_STORE", "BANK_TRANSFER"];

    public GuestCheckoutRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("決済方法は必須です").WithErrorCode("GUEST-V001")
            .Must(m => ValidPaymentMethods.Contains(m))
            .WithMessage("サポートされていない決済方法です").WithErrorCode("GUEST-V002");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です").WithErrorCode("GUEST-V003")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください").WithErrorCode("GUEST-V004")
            .MaximumLength(255).WithErrorCode("GUEST-V005");

        RuleFor(x => x.ShippingAddress)
            .NotNull().WithMessage("配送先は必須です").WithErrorCode("GUEST-V006");

        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress!.RecipientName)
                .NotEmpty().WithMessage("受取人名は必須です").WithErrorCode("GUEST-V010")
                .MaximumLength(100).WithErrorCode("GUEST-V011");

            RuleFor(x => x.ShippingAddress!.PostalCode)
                .NotEmpty().WithMessage("郵便番号は必須です").WithErrorCode("GUEST-V012")
                .Matches(@"^\d{3}-?\d{4}$").WithMessage("郵便番号の形式が不正です").WithErrorCode("GUEST-V013");

            RuleFor(x => x.ShippingAddress!.PhoneNumber)
                .NotEmpty().WithMessage("電話番号は必須です").WithErrorCode("GUEST-V014")
                .Matches(@"^[\d\-]+$").WithMessage("電話番号の形式が不正です").WithErrorCode("GUEST-V015");
        });
    }
}
