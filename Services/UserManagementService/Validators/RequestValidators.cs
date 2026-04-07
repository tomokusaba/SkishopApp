using FluentValidation;
using UserManagementService.DTOs.Requests;
using UserManagementService.Models;

namespace UserManagementService.Validators;

/// <summary>ユーザープロフィール更新リクエストのバリデータ。誕生日は過去日のみ許可。</summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null);

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[\d\-+()]{1,20}$")
            .When(x => x.PhoneNumber is not null)
            .WithMessage("電話番号の形式が無効です");

        RuleFor(x => x.BirthDate)
            .LessThan(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .When(x => x.BirthDate is not null)
            .WithMessage("誕生日は過去日を指定してください");
    }
}

/// <summary>住所作成リクエストのバリデータ。全必須フィールドと郵便番号・電話番号の形式検証を行う。</summary>
public class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.AddressType)
            .NotEmpty()
            .Must(t => t is AddressType.Shipping or AddressType.Billing)
            .WithMessage("住所種別は SHIPPING または BILLING を指定してください");

        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ZipCode).NotEmpty().Matches(@"^\d{3}-?\d{4}$")
            .WithMessage("郵便番号の形式が無効です（例: 123-4567）");
        RuleFor(x => x.Prefecture).NotEmpty().MaximumLength(50);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StreetAddress).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Building).MaximumLength(255);
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[\d\-+()]{1,20}$")
            .When(x => x.PhoneNumber is not null)
            .WithMessage("電話番号の形式が無効です");
    }
}

/// <summary>住所更新リクエストのバリデータ。全フィールドがオプショナル（PATCH 的更新）。</summary>
public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.Recipient).MaximumLength(100).When(x => x.Recipient is not null);
        RuleFor(x => x.ZipCode).Matches(@"^\d{3}-?\d{4}$")
            .When(x => x.ZipCode is not null)
            .WithMessage("郵便番号の形式が無効です");
        RuleFor(x => x.Prefecture).MaximumLength(50).When(x => x.Prefecture is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.StreetAddress).MaximumLength(255).When(x => x.StreetAddress is not null);
        RuleFor(x => x.Building).MaximumLength(255).When(x => x.Building is not null);
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^[\d\-+()]{1,20}$")
            .When(x => x.PhoneNumber is not null)
            .WithMessage("電話番号の形式が無効です");
    }
}

/// <summary>ウィッシュリスト作成リクエストのバリデータ。名前は 1〜100 文字。</summary>
public class CreateWishlistRequestValidator : AbstractValidator<CreateWishlistRequest>
{
    public CreateWishlistRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .WithMessage("名前は1〜100文字で入力してください");
    }
}

/// <summary>ユーザー設定更新リクエストのバリデータ。言語は ja/en、通貨は JPY/USD のみ。</summary>
public class UpdatePreferenceRequestValidator : AbstractValidator<UpdatePreferenceRequest>
{
    public UpdatePreferenceRequestValidator()
    {
        RuleFor(x => x.Language)
            .Must(l => l is "ja" or "en")
            .When(x => x.Language is not null)
            .WithMessage("言語は ja または en を指定してください");

        RuleFor(x => x.Currency)
            .Must(c => c is "JPY" or "USD")
            .When(x => x.Currency is not null)
            .WithMessage("通貨は JPY または USD を指定してください");
    }
}

/// <summary>同意更新リクエストのバリデータ。同意種別の値検証とポリシーバージョン > 0 を確認する。</summary>
public class ConsentUpdateRequestValidator : AbstractValidator<ConsentUpdateRequest>
{
    public ConsentUpdateRequestValidator()
    {
        RuleFor(x => x.ConsentType)
            .NotEmpty()
            .Must(t => t is ConsentType.Marketing
                or ConsentType.Personalization
                or ConsentType.Analytics
                or ConsentType.ThirdPartySharing)
            .WithMessage("同意種別が無効です");

        RuleFor(x => x.PolicyVersion)
            .GreaterThan(0)
            .WithMessage("ポリシーバージョンは 1 以上を指定してください");
    }
}

/// <summary>ウィッシュリスト更新リクエストのバリデータ。名前は 100 文字以内。</summary>
public class UpdateWishlistRequestValidator : AbstractValidator<UpdateWishlistRequest>
{
    public UpdateWishlistRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => x.Name is not null)
            .WithMessage("ウィッシュリスト名は 100 文字以内で入力してください");
    }
}

/// <summary>ウィッシュリストアイテム追加リクエストのバリデータ。商品 ID は必須・36 文字以内。</summary>
public class AddWishlistItemRequestValidator : AbstractValidator<AddWishlistItemRequest>
{
    public AddWishlistItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("商品 ID は必須です")
            .MaximumLength(36)
            .WithMessage("商品 ID は 36 文字以内で入力してください");
    }
}

/// <summary>ユーザーステータス更新リクエストのバリデータ。有効なステータス値のホワイトリスト検証を行う。</summary>
public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    private static readonly string[] ValidStatuses = ["PENDING_VERIFICATION", "ACTIVE", "SUSPENDED", "DEACTIVATED"];

    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("ステータスは必須です")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("ステータスは PENDING_VERIFICATION, ACTIVE, SUSPENDED, DEACTIVATED のいずれかを指定してください");
    }
}

/// <summary>処理制限更新リクエストのバリデータ。制限有効時は理由必須。</summary>
public class UpdateProcessingRestrictionRequestValidator : AbstractValidator<UpdateProcessingRestrictionRequest>
{
    public UpdateProcessingRestrictionRequestValidator()
    {
        RuleFor(x => x.RestrictionReason)
            .NotEmpty()
            .When(x => x.IsProcessingRestricted)
            .WithMessage("処理制限を設定する場合、理由は必須です")
            .MaximumLength(500)
            .When(x => x.RestrictionReason is not null)
            .WithMessage("制限理由は 500 文字以内で入力してください");
    }
}

/// <summary>削除リクエスト作成のバリデータ。リクエストチャネルのホワイトリスト検証を行う。</summary>
public class CreateDeletionRequestValidator : AbstractValidator<CreateDeletionRequest>
{
    private static readonly string[] ValidChannels = ["WEB_SELF_SERVICE", "ADMIN_CONSOLE", "EMAIL_DSR", "API"];

    public CreateDeletionRequestValidator()
    {
        RuleFor(x => x.RequestChannel)
            .NotEmpty()
            .WithMessage("リクエストチャネルは必須です")
            .MaximumLength(30)
            .WithMessage("リクエストチャネルは 30 文字以内で入力してください")
            .Must(c => ValidChannels.Contains(c))
            .WithMessage("リクエストチャネルは WEB_SELF_SERVICE, ADMIN_CONSOLE, EMAIL_DSR, API のいずれかを指定してください");
    }
}
