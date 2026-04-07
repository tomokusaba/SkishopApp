using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="SizeGuideUpdateRequest"/> のバリデーター。
/// サイズガイド更新リクエストの入力値を検証する。
/// </summary>
/// <remarks>
/// 検証ルール: 各フィールドは指定時のみ検証（部分更新対応）。
/// ガイド種別は最大 50 文字、サイズ表は空文字不可。
/// </remarks>
public class SizeGuideUpdateRequestValidator : AbstractValidator<SizeGuideUpdateRequest>
{
    public SizeGuideUpdateRequestValidator()
    {
        RuleFor(x => x.GuideType)
            .MaximumLength(50)
            .When(x => x.GuideType is not null);

        RuleFor(x => x.SizeChart)
            .NotEmpty()
            .When(x => x.SizeChart is not null);
    }
}
