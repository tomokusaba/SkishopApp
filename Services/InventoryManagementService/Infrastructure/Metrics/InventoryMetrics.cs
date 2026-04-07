using System.Diagnostics.Metrics;

namespace InventoryManagementService.Infrastructure.Metrics;

/// <summary>
/// 在庫管理サービスのカスタムビジネスメトリクス。
/// OpenTelemetry 経由で Prometheus / Azure Monitor 等に公開される。
/// </summary>
public sealed class InventoryMetrics : IDisposable
{
    private readonly Meter _meter;
    
    /// <summary>
    /// 在庫数量ゲージ。商品ごとの現在在庫数を記録する。
    /// spec.md 定義: inventory.stock_level (Gauge, 単位: 個)
    /// </summary>
    public ObservableGauge<int>? StockLevel { get; private set; }
    
    /// <summary>
    /// 在庫予約処理の成功/失敗カウンター。
    /// </summary>
    public Counter<long> ReservationAttempts { get; }
    
    /// <summary>
    /// 低在庫アラート発火回数カウンター。
    /// </summary>
    public Counter<long> LowStockAlerts { get; }

    public InventoryMetrics()
    {
        _meter = new Meter("SkiShop.InventoryManagementService", "1.0.0");
        
        ReservationAttempts = _meter.CreateCounter<long>(
            "inventory.reservation_attempts",
            unit: "{attempt}",
            description: "在庫予約試行回数（成功/失敗別）");

        LowStockAlerts = _meter.CreateCounter<long>(
            "inventory.low_stock_alerts",
            unit: "{alert}",
            description: "低在庫アラート発火回数");
    }

    /// <summary>
    /// 在庫レベルのオブザーバブルゲージを登録する。
    /// コールバックが呼ばれるたびに全商品の在庫数を返す。
    /// </summary>
    public void RegisterStockLevelGauge(Func<IEnumerable<Measurement<int>>> observeValues)
    {
        StockLevel = _meter.CreateObservableGauge(
            "inventory.stock_level",
            observeValues,
            unit: "{item}",
            description: "商品在庫数（低在庫アラート用）");
    }

    public void Dispose() => _meter.Dispose();
}
