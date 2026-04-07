namespace CouponService.Models;

public enum DiscountType
{
    FixedAmount = 0,
    Percentage = 1,
    FreeShipping = 2
}

public enum CouponStatus
{
    Active = 0,
    Inactive = 1,
    Expired = 2,
    Depleted = 3
}

public enum CampaignStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Ended = 3
}

public enum OutboxEventStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    Failed = 3,
    DeadLetter = 4
}

public enum UserCouponStatus
{
    Available = 0,
    Used = 1,
    Expired = 2
}

public enum UsageLimitationType
{
    SingleUse = 0,
    MultiUse = 1,
    TimeLimited = 2
}
