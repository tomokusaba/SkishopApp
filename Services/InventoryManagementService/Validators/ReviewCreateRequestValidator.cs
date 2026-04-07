using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="ReviewCreateRequest"/> のバリデーター。
/// レビュー新規作成リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール:
/// <list type="bullet">
///   <item>商品 ID は必須</item>
///   <item>評価は 1〜5 の範囲</item>
///   <item>タイトルは必須、最大 255 文字</item>
///   <item>本文は最大 5000 文字（指定時のみ）</item>
/// </list>
/// </remarks>
public class ReviewCreateRequestValidator : AbstractValidator<ReviewCreateRequest>
{
    public ReviewCreateRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5)
            .WithMessage("評価は 1〜5 の整数を指定してください");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Content).MaximumLength(5000).When(x => x.Content is not null);
    }
}
