using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// <see cref="BatchIdsRequest"/> のバリデーター。
/// 一括取得 API で指定される ID リストを検証する。
/// </summary>
/// <remarks>
/// 検証ルール:
/// <list type="bullet">
///   <item>ID リストは null・空でないこと（1 件以上）</item>
///   <item>ID リストの上限は 50 件</item>
///   <item>各 ID は空文字でないこと</item>
///   <item>各 ID は 36 文字以内（UUID 形式を想定）</item>
/// </list>
/// </remarks>
public class BatchIdsRequestValidator : AbstractValidator<BatchIdsRequest>
{
    public BatchIdsRequestValidator()
    {
        RuleFor(x => x.Ids)
            .NotNull().WithMessage("ID リストは必須です")
            .NotEmpty().WithMessage("ID リストは 1 件以上指定してください")
            .Must(ids => ids is null || ids.Count <= 50).WithMessage("一括取得の上限は 50 件です");

        RuleForEach(x => x.Ids)
            .NotEmpty().WithMessage("ID は空文字にできません")
            .MaximumLength(36).WithMessage("ID は 36 文字以内で指定してください");
    }
}
