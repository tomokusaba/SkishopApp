using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// ProductSearchParams のバリデーター。
/// 商品検索パラメータの入力検証を行う。
/// </summary>
public class ProductSearchParamsValidator : AbstractValidator<ProductSearchParams>
{
    private static readonly string[] AllowedSortFields = ["CreatedAt", "Name", "Price", "Rating"];

    public ProductSearchParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ページ番号は 0 以上である必要があります");

        RuleFor(x => x.Size)
            .InclusiveBetween(1, 100)
            .WithMessage("ページサイズは 1〜100 の範囲である必要があります");

        RuleFor(x => x.Keyword)
            .MaximumLength(100)
            .When(x => x.Keyword is not null)
            .WithMessage("キーワードは 100 文字以内で入力してください");

        RuleFor(x => x.SortBy)
            .Must(s => AllowedSortFields.Contains(s))
            .WithMessage("ソートフィールドは CreatedAt, Name, Price, Rating のいずれかです");

        RuleFor(x => x.CategoryId)
            .MaximumLength(36)
            .When(x => x.CategoryId is not null)
            .WithMessage("カテゴリ ID は 36 文字以内で入力してください");

        RuleFor(x => x.Brand)
            .MaximumLength(100)
            .When(x => x.Brand is not null)
            .WithMessage("ブランドは 100 文字以内で入力してください");

        RuleFor(x => x.Category)
            .MaximumLength(255)
            .When(x => x.Category is not null)
            .WithMessage("カテゴリ名は 255 文字以内で入力してください");
    }
}
