using System.Diagnostics.Metrics;

namespace CouponService.Infrastructure.Observability;

public class CouponMetrics
{
    public static readonly Meter CouponMeter = new("SkiShop.CouponService", "1.0");

    public static readonly Counter<long> ValidationCount =
        CouponMeter.CreateCounter<long>("coupon.validation.count",
            description: "クーポンバリデーション実行回数");

    public static readonly Counter<long> AppliedCount =
        CouponMeter.CreateCounter<long>("coupon.applied.count",
            description: "クーポン適用回数");

    public static readonly Counter<long> FraudDetectionCount =
        CouponMeter.CreateCounter<long>("coupon.fraud_detection.count",
            description: "不正検知回数");

    public static readonly Histogram<double> RuleEngineLatency =
        CouponMeter.CreateHistogram<double>("coupon.rule_engine.duration_ms",
            description: "ルールエンジン実行時間（ミリ秒）");

    public static readonly Counter<long> CacheHitCount =
        CouponMeter.CreateCounter<long>("coupon.cache.hit_count",
            description: "キャッシュヒット回数");

    public static readonly Counter<long> CacheMissCount =
        CouponMeter.CreateCounter<long>("coupon.cache.miss_count",
            description: "キャッシュミス回数");

    public static readonly Histogram<double> DiscountAmount =
        CouponMeter.CreateHistogram<double>("coupon.discount.amount",
            description: "適用された割引額の分布");

    public static readonly Counter<long> RedeemedTotal =
        CouponMeter.CreateCounter<long>("coupon.redeemed.total",
            description: "クーポン利用確定回数");
}
