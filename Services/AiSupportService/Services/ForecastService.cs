using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IForecastService"/> の実装。移動平均法に基づく需要予測を生成する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、過去の販売データに基づいて将来の商品需要を予測します。
/// 現在の実装では、単純移動平均法（Simple Moving Average）を使用しています。
/// </para>
/// <para>
/// 予測アルゴリズムの詳細:
/// <list type="bullet">
///   <item><description>過去 10 件の予測データを取得</description></item>
///   <item><description>同一予測期間のデータが 3 件以上ある場合は移動平均を計算</description></item>
///   <item><description>データが不足している場合は期間ごとのデフォルト値を使用</description></item>
///   <item><description>信頼度は変動係数（CV）から算出</description></item>
/// </list>
/// </para>
/// <para>
/// 将来的な拡張:
/// <list type="bullet">
///   <item><description>ARIMA / Prophet などの高度な時系列モデルの導入</description></item>
///   <item><description>季節性・トレンド成分の分解</description></item>
///   <item><description>外部要因（天候、イベントなど）の考慮</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="forecastRepository">需要予測リポジトリ。</param>
/// <param name="metrics">AI サポートメトリクス。</param>
/// <param name="logger">ロガー。</param>
public class ForecastService(
    IDemandForecastRepository forecastRepository,
    AiSupportMetrics metrics,
    ILogger<ForecastService> logger) : IForecastService
{
    /// <summary>週次のデフォルト需要予測値。</summary>
    private const int DefaultWeeklyDemand = 50;

    /// <summary>月次のデフォルト需要予測値。</summary>
    private const int DefaultMonthlyDemand = 200;

    /// <summary>四半期のデフォルト需要予測値。</summary>
    private const int DefaultQuarterlyDemand = 600;

    /// <summary>年次のデフォルト需要予測値。</summary>
    private const int DefaultYearlyDemand = 2400;

    /// <summary>履歴データ不足時のデフォルト信頼度スコア。</summary>
    private const decimal DefaultConfidence = 0.75m;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 予測生成の処理フロー:
    /// <list type="number">
    ///   <item><description>対象商品の過去 10 件の予測履歴を取得</description></item>
    ///   <item><description>移動平均法で予測需要と信頼度を計算</description></item>
    ///   <item><description>予測結果をデータベースに保存</description></item>
    ///   <item><description>予測生成メトリクスを記録</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// モデルバージョンは "moving-average-v1" として記録されます。
    /// </para>
    /// </remarks>
    public async Task<ForecastResponse> GenerateAsync(
        GenerateForecastRequest request, string adminUserId, CancellationToken ct = default)
    {
        var historicalData = await forecastRepository.FindByProductIdRecentAsync(request.ProductId, 10, ct);

        var (predictedDemand, confidence) = CalculateForecast(request.ForecastPeriod, historicalData);

        var forecast = new DemandForecast
        {
            ProductId = request.ProductId,
            Sku = request.Sku,
            ForecastDate = DateTime.UtcNow,
            ForecastPeriod = request.ForecastPeriod,
            PredictedDemand = predictedDemand,
            ConfidenceScore = confidence,
            ModelVersion = "moving-average-v1"
        };

        await forecastRepository.AddAsync(forecast, ct);
        await forecastRepository.SaveChangesAsync(ct);

        metrics.RecordForecastGenerated();
        logger.LogInformation("需要予測生成: ProductId={ProductId}, Period={Period}, Demand={Demand}, Confidence={Confidence}",
            request.ProductId, request.ForecastPeriod, predictedDemand, confidence);

        return ToResponse(forecast);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 結果は予測日時の降順（新しい順）でソートされます。
    /// </remarks>
    public async Task<List<ForecastResponse>> GetByProductIdAsync(
        string productId, CancellationToken ct = default)
    {
        var forecasts = await forecastRepository.FindByProductIdAsync(productId, ct);
        return forecasts.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 結果は予測日時の降順（新しい順）でソートされます。
    /// </remarks>
    public async Task<List<ForecastResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var forecasts = await forecastRepository.FindAllAsync(ct);
        return forecasts.Select(ToResponse).ToList();
    }

    /// <summary>
    /// 履歴データに基づき移動平均法で需要予測を計算する。
    /// </summary>
    /// <param name="period">予測期間（WEEKLY, MONTHLY, QUARTERLY, YEARLY）。</param>
    /// <param name="historicalData">過去の予測データのリスト。</param>
    /// <returns>予測需要数と信頼度スコアのタプル。</returns>
    /// <remarks>
    /// <para>
    /// 計算ロジック:
    /// <list type="number">
    ///   <item><description>同一期間の履歴データをフィルタリング</description></item>
    ///   <item><description>履歴が 3 件未満の場合はデフォルト値を返却</description></item>
    ///   <item><description>履歴が 3 件以上の場合は移動平均を計算</description></item>
    ///   <item><description>信頼度は変動係数（CV）から算出: confidence = 1.0 - CV</description></item>
    ///   <item><description>信頼度は 0.5〜0.95 の範囲にクランプ</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 変動係数（CV: Coefficient of Variation）は標準偏差を平均で割った値で、
    /// データのばらつきを表します。CV が大きいほど信頼度は低くなります。
    /// </para>
    /// </remarks>
    private static (int PredictedDemand, decimal Confidence) CalculateForecast(
        string period, List<DemandForecast> historicalData)
    {
        var relevantHistory = historicalData
            .Where(h => h.ForecastPeriod == period)
            .ToList();

        if (relevantHistory.Count < 3)
        {
            var defaultDemand = GetDefaultDemand(period);
            return (defaultDemand, DefaultConfidence);
        }

        var demands = relevantHistory.Select(h => h.PredictedDemand).ToList();
        var movingAverage = (int)Math.Round(demands.Average());

        var variance = demands.Select(d => Math.Pow(d - movingAverage, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        var cv = movingAverage > 0 ? stdDev / movingAverage : 1.0;
        var confidence = Math.Max(0.5m, Math.Min(0.95m, 1.0m - (decimal)cv));

        return (movingAverage, Math.Round(confidence, 2));
    }

    /// <summary>
    /// 予測期間に応じたデフォルト需要値を返す。
    /// </summary>
    /// <param name="period">予測期間（WEEKLY, MONTHLY, QUARTERLY, YEARLY）。</param>
    /// <returns>デフォルトの予測需要数。</returns>
    /// <remarks>
    /// 認識できない期間の場合は月次のデフォルト値（200）を返します。
    /// </remarks>
    private static int GetDefaultDemand(string period) => period.ToUpperInvariant() switch
    {
        "WEEKLY" => DefaultWeeklyDemand,
        "MONTHLY" => DefaultMonthlyDemand,
        "QUARTERLY" => DefaultQuarterlyDemand,
        "YEARLY" => DefaultYearlyDemand,
        _ => DefaultMonthlyDemand
    };

    /// <summary>
    /// <see cref="DemandForecast"/> エンティティをレスポンス DTO に変換する。
    /// </summary>
    /// <param name="f">変換元の需要予測エンティティ。</param>
    /// <returns>需要予測レスポンス DTO。</returns>
    private static ForecastResponse ToResponse(DemandForecast f)
        => new(f.Id, f.ProductId, f.Sku, f.ForecastDate, f.ForecastPeriod,
               f.PredictedDemand, f.ConfidenceScore, f.ModelVersion, f.CreatedAt);
}
