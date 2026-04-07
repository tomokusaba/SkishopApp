namespace PointService.Models;

/// <summary>ポイント取引種別。</summary>
public static class TransactionTypes
{
    public const string Earn = "EARN";
    public const string Redeem = "REDEEM";
    public const string Expire = "EXPIRE";
    public const string Adjust = "ADJUST";
    public const string Reserve = "RESERVE";
    public const string Release = "RELEASE";
    public const string Refund = "REFUND";
    public const string Cancel = "CANCEL";
}

/// <summary>ポイント有効期限ステータス。</summary>
public static class ExpiryStatuses
{
    public const string Active = "ACTIVE";
    public const string Expired = "EXPIRED";
    public const string Consumed = "CONSUMED";
}

/// <summary>Outbox イベントステータス。</summary>
public static class OutboxStatuses
{
    public const string Pending = "PENDING";
    public const string Published = "PUBLISHED";
    public const string Failed = "FAILED";
}

/// <summary>参照元種別。</summary>
public static class ReferenceTypes
{
    public const string Order = "ORDER";
    public const string AdminAdjust = "ADMIN_ADJUST";
    public const string Expiry = "EXPIRY";
}

/// <summary>Kafka イベントトピック。</summary>
public static class EventTopics
{
    public const string PointEarned = "point.earned";
    public const string PointRedeemed = "point.redeemed";
    public const string PointReserved = "point.reserved";
    public const string PointReleased = "point.released";
    public const string PointExpired = "point.expired";
    public const string OrderCreated = "order.created";
    public const string OrderCancelled = "order.cancelled";
    public const string UserRegistered = "user.registered";
    public const string UserDeleted = "user.deleted";
    public const string PaymentRefunded = "payment.refunded";
    public const string MemberRankUpdated = "member_rank.updated";
}

/// <summary>ティアレベル名。</summary>
public static class TierLevels
{
    public const string Bronze = "BRONZE";
    public const string Silver = "SILVER";
    public const string Gold = "GOLD";
    public const string Platinum = "PLATINUM";
}

/// <summary>監査ログアクション。</summary>
public static class AuditActions
{
    public const string Add = "ADD";
    public const string Subtract = "SUBTRACT";
    public const string Adjust = "ADJUST";
}

/// <summary>Aggregate タイプ。</summary>
public static class AggregateTypes
{
    public const string PointAccount = "PointAccount";
}
