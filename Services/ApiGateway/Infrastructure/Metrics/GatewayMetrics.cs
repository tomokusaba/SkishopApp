using System.Diagnostics.Metrics;

namespace ApiGateway.Infrastructure.Metrics;

/// <summary>
/// API Gateway のカスタムメトリクスを DI 経由で提供するシングルトンサービス。
/// <see cref="IMeterFactory"/> を使用してメーターを生成し、
/// OpenTelemetry のリクエスト統計・レート制限・サーキットブレーカーの監視に利用される。
/// </summary>
public sealed class GatewayMetrics
{
    /// <summary>OpenTelemetry メーター名（<c>.AddMeter()</c> 登録用定数）。</summary>
    public const string MeterName = "SkiShop.ApiGateway";

    /// <summary>
    /// <see cref="IMeterFactory"/> を使用してメーターとインストゥルメントを初期化する。
    /// </summary>
    /// <param name="meterFactory">OpenTelemetry メーターファクトリ。</param>
    public GatewayMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, "1.0.0");

        RequestsTotal = meter.CreateCounter<long>(
            "gateway.requests.total", description: "Total gateway requests");
        RequestDuration = meter.CreateHistogram<double>(
            "gateway.requests.duration", "ms", "Request duration in milliseconds");
        RateLimiterLimited = meter.CreateCounter<long>(
            "gateway.rate_limiter.limited", description: "Rate limiter rejection events");
        CircuitBreakerState = meter.CreateUpDownCounter<int>(
            "gateway.circuit_breaker.state", description: "Circuit breaker state changes");
        CircuitBreakerCalls = meter.CreateCounter<long>(
            "gateway.circuit_breaker.calls", description: "Circuit breaker call counts");
    }

    /// <summary>
    /// ゲートウェイを通過したリクエストの累積カウンタ。
    /// タグ: <c>method</c>（HTTP メソッド）、<c>status</c>（HTTP ステータスコード）。
    /// </summary>
    public Counter<long> RequestsTotal { get; }

    /// <summary>
    /// リクエスト処理時間のヒストグラム（ミリ秒単位）。
    /// タグ: <c>method</c>（HTTP メソッド）、<c>path</c>（リクエストパス）。
    /// </summary>
    public Histogram<double> RequestDuration { get; }

    /// <summary>
    /// レート制限により拒否されたリクエストの累積カウンタ。
    /// タグ: <c>path</c>（拒否されたリクエストのパス）。
    /// </summary>
    public Counter<long> RateLimiterLimited { get; }

    /// <summary>
    /// サーキットブレーカーの状態変化を記録する UpDownCounter。
    /// タグ: <c>cluster</c>（クラスター ID）、<c>state</c>（Closed/Open/HalfOpen）。
    /// </summary>
    public UpDownCounter<int> CircuitBreakerState { get; }

    /// <summary>
    /// サーキットブレーカーの呼び出し結果を記録するカウンタ。
    /// タグ: <c>cluster</c>（クラスター ID）、<c>result</c>（success/failure/rejected）。
    /// </summary>
    public Counter<long> CircuitBreakerCalls { get; }
}
