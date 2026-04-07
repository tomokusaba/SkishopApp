using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// PaginationParams のバリデーター。
/// ページネーションパラメータの入力検証を行う。
/// </summary>
public class PaginationParamsValidator : AbstractValidator<PaginationParams>
{
    public PaginationParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ページ番号は 0 以上である必要があります");

        RuleFor(x => x.Size)
            .InclusiveBetween(1, 100)
            .WithMessage("ページサイズは 1〜100 の範囲である必要があります");
    }
}
