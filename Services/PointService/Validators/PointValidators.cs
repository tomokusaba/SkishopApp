using FluentValidation;
using PointService.DTOs.Requests;

namespace PointService.Validators;

public class AdjustPointsRequestValidator : AbstractValidator<AdjustPointsRequest>
{
    private const int MaxAdjustmentPoints = 100_000;

    public AdjustPointsRequestValidator()
    {
        RuleFor(x => x.Points)
            .NotEqual(0).WithMessage("ポイント数は 0 以外である必要があります")
            .Must(p => Math.Abs(p) <= MaxAdjustmentPoints)
            .WithMessage($"1 回の調整は {MaxAdjustmentPoints:N0} ポイントが上限です");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("調整理由は必須です")
            .MaximumLength(500);

        RuleFor(x => x.ReferenceId)
            .MaximumLength(36).When(x => x.ReferenceId is not null);
    }
}

public class ReservePointsRequestValidator : AbstractValidator<ReservePointsRequest>
{
    private const int MaxRedeemPoints = 500_000;

    public ReservePointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.Points)
            .GreaterThan(0).LessThanOrEqualTo(MaxRedeemPoints);
    }
}

public class AwardPointsRequestValidator : AbstractValidator<AwardPointsRequest>
{
    public AwardPointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderAmount).GreaterThan(0);
    }
}

public class UpdateTierRequestValidator : AbstractValidator<UpdateTierRequest>
{
    public UpdateTierRequestValidator()
    {
        RuleFor(x => x.PointRate)
            .InclusiveBetween(0.001m, 1.0m)
            .WithMessage("ポイントレートは 0.001〜1.0 の範囲で指定してください");

        RuleFor(x => x.MinAnnualPoints)
            .GreaterThanOrEqualTo(0)
            .WithMessage("最小年間ポイントは 0 以上で指定してください");

        RuleFor(x => x.Benefits)
            .MaximumLength(2000).When(x => x.Benefits is not null);
    }
}

public class PaginationQueryValidator : AbstractValidator<PaginationQuery>
{
    public PaginationQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("ページ番号は 1 以上で指定してください");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("ページサイズは 1〜100 の範囲で指定してください");
    }
}

public class ConfirmPointsRequestValidator : AbstractValidator<ConfirmPointsRequest>
{
    public ConfirmPointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
    }
}

public class ReleasePointsRequestValidator : AbstractValidator<ReleasePointsRequest>
{
    public ReleasePointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
    }
}

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
    }
}
