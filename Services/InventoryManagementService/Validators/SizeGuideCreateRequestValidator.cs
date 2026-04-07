using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="SizeGuideCreateRequest"/> のバリデーター。
/// サイズガイド新規作成リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: カテゴリ ID・サイズ表・ガイド種別は全て必須。ガイド種別は最大 50 文字。
/// </remarks>
public class SizeGuideCreateRequestValidator : AbstractValidator<SizeGuideCreateRequest>
{
    public SizeGuideCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.SizeChart).NotEmpty();
        RuleFor(x => x.GuideType).NotEmpty().MaximumLength(50);
    }
}
