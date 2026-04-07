using AiSupportService.DTOs.Requests;
using FluentValidation;

namespace AiSupportService.Validators;

/// <summary>
/// <see cref="SendMessageRequest"/> のバリデーター。メッセージ本文の必須・長さチェックを行う。
/// </summary>
public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("メッセージは必須です")
            .MaximumLength(4000).WithMessage("メッセージは4000文字以内で入力してください")
            .MinimumLength(1).WithMessage("メッセージは1文字以上で入力してください");
    }
}

/// <summary>
/// <see cref="SearchRequest"/> のバリデーター。検索クエリ・価格範囲・ページネーションの整合性を検証する。
/// </summary>
public class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("検索クエリは必須です")
            .MaximumLength(500).WithMessage("検索クエリは500文字以内で入力してください");

        RuleFor(x => x.Category)
            .Matches(@"^[\p{L}\p{N}\s\-]+$").When(x => x.Category is not null)
            .WithMessage("カテゴリに不正な文字が含まれています");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue)
            .WithMessage("最低価格は0以上で入力してください");

        RuleFor(x => x.MaxPrice)
            .GreaterThan(x => x.MinPrice ?? 0).When(x => x.MaxPrice.HasValue && x.MinPrice.HasValue)
            .WithMessage("最高価格は最低価格より大きい値を入力してください");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("ページ番号は1以上で入力してください");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("ページサイズは1〜100の範囲で入力してください");
    }
}

/// <summary>
/// <see cref="RecommendationFeedbackRequest"/> のバリデーター。フィードバック種別のホワイトリスト検証を行う。
/// </summary>
public class RecommendationFeedbackRequestValidator : AbstractValidator<RecommendationFeedbackRequest>
{
    private static readonly string[] AllowedFeedbackTypes = ["CLICK", "PURCHASE", "DISMISS", "LIKE", "DISLIKE"];

    public RecommendationFeedbackRequestValidator()
    {
        RuleFor(x => x.RecommendationId)
            .NotEmpty().WithMessage("レコメンデーションIDは必須です")
            .MaximumLength(36);

        RuleFor(x => x.FeedbackType)
            .NotEmpty().WithMessage("フィードバックタイプは必須です")
            .Must(t => AllowedFeedbackTypes.Contains(t))
            .WithMessage($"フィードバックタイプは {string.Join(", ", AllowedFeedbackTypes)} のいずれかを指定してください");

        RuleFor(x => x.ProductId)
            .MaximumLength(36).When(x => x.ProductId is not null);
    }
}

/// <summary>
/// <see cref="SearchFeedbackRequest"/> のバリデーター。検索 ID と商品 ID の必須・長さチェックを行う。
/// </summary>
public class SearchFeedbackRequestValidator : AbstractValidator<SearchFeedbackRequest>
{
    public SearchFeedbackRequestValidator()
    {
        RuleFor(x => x.SearchId)
            .NotEmpty().WithMessage("検索IDは必須です")
            .MaximumLength(36);

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("商品IDは必須です")
            .MaximumLength(36);
    }
}

/// <summary>
/// <see cref="CreateChatSessionRequest"/> のバリデーター。タイトルの長さチェックを行う。
/// </summary>
public class CreateChatSessionRequestValidator : AbstractValidator<CreateChatSessionRequest>
{
    public CreateChatSessionRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(200).When(x => x.Title is not null)
            .WithMessage("タイトルは200文字以内で入力してください");
    }
}

/// <summary>
/// <see cref="GenerateForecastRequest"/> のバリデーター。予測期間のホワイトリスト検証を行う。
/// </summary>
public class GenerateForecastRequestValidator : AbstractValidator<GenerateForecastRequest>
{
    private static readonly string[] AllowedPeriods = ["WEEKLY", "MONTHLY", "QUARTERLY", "YEARLY"];

    public GenerateForecastRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("商品IDは必須です")
            .MaximumLength(36);

        RuleFor(x => x.ForecastPeriod)
            .NotEmpty().WithMessage("予測期間は必須です")
            .Must(p => AllowedPeriods.Contains(p))
            .WithMessage($"予測期間は {string.Join(", ", AllowedPeriods)} のいずれかを指定してください");

        RuleFor(x => x.Sku)
            .MaximumLength(50).When(x => x.Sku is not null);
    }
}
