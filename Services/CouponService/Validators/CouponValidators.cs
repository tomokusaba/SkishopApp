using CouponService.DTOs.Requests;
using FluentValidation;

namespace CouponService.Validators;

public class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("クーポンコードは必須です")
            .MaximumLength(30).WithMessage("クーポンコードは30文字以内で入力してください")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("クーポンコードは英大文字・数字・ハイフンのみ使用可能です");

        RuleFor(x => x.CouponTypeId)
            .NotEmpty().WithMessage("クーポンタイプIDは必須です");

        RuleFor(x => x.DiscountType)
            .InclusiveBetween(0, 2).WithMessage("割引タイプは0(固定額)、1(パーセント)、2(送料無料)を指定してください");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("割引値は0より大きい値を指定してください");

        When(x => x.DiscountType == 1, () =>
        {
            RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100).WithMessage("パーセント割引は100%以下を指定してください");
        });

        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0).WithMessage("最低注文金額は0以上を指定してください");

        RuleFor(x => x.MaxUsageCount)
            .GreaterThan(0).WithMessage("最大利用回数は1以上を指定してください");

        RuleFor(x => x.MaxUsagePerUser)
            .GreaterThan(0).WithMessage("ユーザーあたり最大利用回数は1以上を指定してください");

        RuleFor(x => x.ValidFrom)
            .NotEmpty().WithMessage("有効開始日は必須です");

        RuleFor(x => x.ValidUntil)
            .NotEmpty().WithMessage("有効終了日は必須です")
            .GreaterThan(x => x.ValidFrom).WithMessage("有効終了日は有効開始日より後を指定してください");
    }
}

public class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("キャンペーン名は必須です")
            .MaximumLength(200).WithMessage("キャンペーン名は200文字以内で入力してください");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("説明は2000文字以内で入力してください");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("開始日は必須です");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("終了日は必須です")
            .GreaterThan(x => x.StartDate).WithMessage("終了日は開始日より後を指定してください");

        RuleFor(x => x.MaxCoupons)
            .GreaterThan(0).WithMessage("最大発行数は1以上を指定してください");
    }
}

public class ValidateCouponRequestValidator : AbstractValidator<ValidateCouponRequest>
{
    public ValidateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("クーポンコードは必須です")
            .MaximumLength(30).WithMessage("クーポンコードは30文字以内で入力してください");

        RuleFor(x => x.OrderAmount)
            .GreaterThan(0).WithMessage("注文金額は0より大きい値を指定してください");
    }
}

public class RedeemCouponRequestValidator : AbstractValidator<RedeemCouponRequest>
{
    public RedeemCouponRequestValidator()
    {
        RuleFor(x => x.CouponId)
            .NotEmpty().WithMessage("クーポンIDは必須です");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("オーダーIDは必須です");

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0).WithMessage("割引額は0より大きい値を指定してください");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ユーザーIDは必須です");
    }
}

public class CalculateDiscountRequestValidator : AbstractValidator<CalculateDiscountRequest>
{
    public CalculateDiscountRequestValidator()
    {
        RuleFor(x => x.CouponCode)
            .NotEmpty().WithMessage("クーポンコードは必須です");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ユーザーIDは必須です");

        RuleFor(x => x.OrderAmount)
            .GreaterThan(0).WithMessage("注文金額は0より大きい値を指定してください");
    }
}

public class UpdateCouponRequestValidator : AbstractValidator<UpdateCouponRequest>
{
    public UpdateCouponRequestValidator()
    {
        When(x => x.DiscountType.HasValue, () =>
        {
            RuleFor(x => x.DiscountType!.Value)
                .InclusiveBetween(0, 2).WithMessage("割引タイプは0(固定額)、1(パーセント)、2(送料無料)を指定してください");
        });

        When(x => x.DiscountValue.HasValue, () =>
        {
            RuleFor(x => x.DiscountValue!.Value)
                .GreaterThan(0).WithMessage("割引値は0より大きい値を指定してください");
        });

        When(x => x.DiscountType == 1 && x.DiscountValue.HasValue, () =>
        {
            RuleFor(x => x.DiscountValue!.Value)
                .LessThanOrEqualTo(100).WithMessage("パーセント割引は100%以下を指定してください");
        });

        When(x => x.MinOrderAmount.HasValue, () =>
        {
            RuleFor(x => x.MinOrderAmount!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("最低注文金額は0以上を指定してください");
        });

        When(x => x.MaxUsageCount.HasValue, () =>
        {
            RuleFor(x => x.MaxUsageCount!.Value)
                .GreaterThan(0).WithMessage("最大利用回数は1以上を指定してください");
        });

        When(x => x.MaxUsagePerUser.HasValue, () =>
        {
            RuleFor(x => x.MaxUsagePerUser!.Value)
                .GreaterThan(0).WithMessage("ユーザーあたり最大利用回数は1以上を指定してください");
        });

        When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue, () =>
        {
            RuleFor(x => x.ValidUntil!.Value)
                .GreaterThan(x => x.ValidFrom!.Value).WithMessage("有効終了日は有効開始日より後を指定してください");
        });
    }
}

public class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator()
    {
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .NotEmpty().WithMessage("キャンペーン名は必須です")
                .MaximumLength(200).WithMessage("キャンペーン名は200文字以内で入力してください");
        });

        When(x => x.Description is not null, () =>
        {
            RuleFor(x => x.Description!)
                .MaximumLength(2000).WithMessage("説明は2000文字以内で入力してください");
        });

        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThan(x => x.StartDate!.Value).WithMessage("終了日は開始日より後を指定してください");
        });

        When(x => x.MaxCoupons.HasValue, () =>
        {
            RuleFor(x => x.MaxCoupons!.Value)
                .GreaterThan(0).WithMessage("最大発行数は1以上を指定してください");
        });
    }
}

public class ReleaseCouponRequestValidator : AbstractValidator<ReleaseCouponRequest>
{
    public ReleaseCouponRequestValidator()
    {
        RuleFor(x => x.CouponId)
            .NotEmpty().WithMessage("クーポンIDは必須です")
            .MaximumLength(36).WithMessage("クーポンIDは36文字以内で入力してください");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("オーダーIDは必須です")
            .MaximumLength(36).WithMessage("オーダーIDは36文字以内で入力してください");
    }
}
