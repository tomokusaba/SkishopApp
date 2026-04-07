using AiSupportService.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiSupportService.Infrastructure.HealthChecks;

/// <summary>
/// Azure AI Search サービスの疎通を確認するヘルスチェック実装。
/// </summary>
/// <remarks>
/// <para>
/// このヘルスチェックは Kubernetes や .NET Aspire の Readiness プローブとして使用され、
/// Azure AI Search への接続状態を検証する。接続不能の場合、サービスはロードバランサーから除外される。
/// </para>
/// <para>
/// 検証方法: "health-check" キーワードで簡易検索リクエストを発行し、レスポンスが返ることで接続状態を判定する。
/// タイムアウト（10 秒）を設定し、長時間のブロッキングを防止する。
/// </para>
/// <para>
/// <b>依存サービス:</b>
/// <list type="bullet">
///   <item><description><see cref="IProductClient"/> - Azure AI Search へのアクセスを提供するクライアント</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs でのヘルスチェック登録
/// builder.Services.AddHealthChecks()
///     .AddCheck&lt;AzureAISearchHealthCheck&gt;("azure-ai-search", tags: ["ready"]);
/// </code>
/// </example>
/// <param name="productClient">Azure AI Search へのアクセスを提供する商品クライアント。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class AzureAISearchHealthCheck(
    IProductClient productClient,
    ILogger<AzureAISearchHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// Azure AI Search に対してヘルスチェック用のダミー検索を実行し、接続状態を検証する。
    /// </summary>
    /// <param name="context">
    /// ヘルスチェックコンテキスト。登録名やタグ情報を含む。
    /// </param>
    /// <param name="ct">
    /// キャンセルトークン。外部からのキャンセル要求を受け付ける。
    /// 内部で 10 秒のタイムアウトも設定される。
    /// </param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description><see cref="HealthCheckResult.Healthy"/> - 検索が正常に完了した場合</description></item>
    ///   <item><description><see cref="HealthCheckResult.Unhealthy"/> - タイムアウトまたは例外発生時</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// "health-check" という固定キーワードで検索を実行するため、実際の商品データには影響しない。
    /// 検索結果の内容は検証せず、レスポンスが返ることのみを確認する。
    /// </remarks>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var results = await productClient.SearchProductsAsync("health-check", ct: cts.Token);
            return HealthCheckResult.Healthy("Azure AI Search に接続可能");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure AI Search ヘルスチェック失敗");
            return HealthCheckResult.Unhealthy("Azure AI Search に接続不可", ex);
        }
    }
}
