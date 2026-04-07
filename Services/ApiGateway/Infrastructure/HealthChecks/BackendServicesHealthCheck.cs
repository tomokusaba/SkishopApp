using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;

namespace ApiGateway.Infrastructure.HealthChecks;

/// <summary>
/// 全バックエンドマイクロサービスの死活監視を行うヘルスチェック。
/// YARP の <see cref="IProxyStateLookup"/> を通じてクラスターの Active Health Check 結果を参照し、
/// 追加の HTTP リクエストを発行せずに各サービスの状態を判定する。
/// <para>
/// Kubernetes Readiness Probe（<c>/health/ready</c>）で使用され、
/// 必須サービス（認証・在庫・販売・決済）の障害時は <see cref="HealthStatus.Unhealthy"/>、
/// 任意サービス（ユーザー管理・クーポン・ポイント・AI）のみの障害時は <see cref="HealthStatus.Degraded"/> を返す。
/// </para>
/// </summary>
/// <param name="proxyStateLookup">YARP クラスター／ルートの実行時状態を参照するサービス。</param>
/// <param name="logger">ヘルスチェック結果のログ出力用ロガー。</param>
public sealed class BackendServicesHealthCheck(
    IProxyStateLookup proxyStateLookup,
    ILogger<BackendServicesHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// クラスター ID と重要度（<c>true</c> = 必須、<c>false</c> = 任意）のマッピング。
    /// 必須クラスターの全 Destination が Unhealthy の場合、ゲートウェイ全体を Unhealthy にする。
    /// </summary>
    private static readonly Dictionary<string, bool> ClusterCriticality = new()
    {
        ["auth-cluster"] = true,
        ["inventory-cluster"] = true,
        ["sales-cluster"] = true,
        ["payment-cart-cluster"] = true,
        ["user-cluster"] = false,
        ["coupons-cluster"] = false,
        ["points-cluster"] = false,
        ["ai-cluster"] = false,
        // R-C2: MailSendService（管理者専用、非必須）
        ["mail-cluster"] = false
    };

    /// <summary>
    /// YARP の <see cref="IProxyStateLookup"/> から全クラスターの状態を取得し、
    /// 各クラスターの Available Destinations 数に基づいて総合判定結果を返す。
    /// </summary>
    /// <param name="context">ヘルスチェックのコンテキスト情報。</param>
    /// <param name="ct">リクエストのキャンセルを伝搬するトークン。</param>
    /// <returns>
    /// 全クラスター正常時は <see cref="HealthCheckResult.Healthy"/>、
    /// 任意クラスターのみ障害時は <see cref="HealthCheckResult.Degraded"/>、
    /// 必須クラスター障害時は <see cref="HealthCheckResult.Unhealthy"/>。
    /// </returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var data = new Dictionary<string, object>();
        var hasCriticalFailure = false;
        var hasOptionalFailure = false;

        foreach (var (clusterId, isCritical) in ClusterCriticality)
        {
            var (status, reason) = EvaluateClusterHealth(clusterId);
            data[clusterId] = reason is not null
                ? new { status, reason }
                : (object)new { status };

            if (status != "Healthy")
            {
                if (isCritical) hasCriticalFailure = true;
                else hasOptionalFailure = true;
            }
        }

        if (hasCriticalFailure)
            return Task.FromResult(
                HealthCheckResult.Unhealthy("必須サービスが利用不可です", data: data));

        if (hasOptionalFailure)
            return Task.FromResult(
                HealthCheckResult.Degraded("一部の非必須サービスが利用不可です", data: data));

        return Task.FromResult(
            HealthCheckResult.Healthy("全バックエンドサービスが正常です", data: data));
    }

    /// <summary>
    /// 指定クラスター ID の YARP 内部ヘルス状態を評価する。
    /// Active Health Check の結果に基づき、利用可能な Destination が存在するかを判定する。
    /// </summary>
    /// <param name="clusterId">評価対象のクラスター ID（例: <c>"auth-cluster"</c>）。</param>
    /// <returns>ステータス文字列と理由（正常時は <c>null</c>）のタプル。</returns>
    private (string Status, string? Reason) EvaluateClusterHealth(string clusterId)
    {
        if (!proxyStateLookup.TryGetCluster(clusterId, out var cluster))
        {
            logger.LogWarning("YARP クラスター未登録: {ClusterId}", clusterId);
            return ("Unknown", "Cluster not configured");
        }

        var allDestinations = cluster.Destinations;
        if (allDestinations is null || allDestinations.Count == 0)
        {
            return ("Unknown", "No destinations configured");
        }

        // YARP Active Health Check が判定済みの Available Destinations を参照
        var availableDestinations = cluster.DestinationsState?.AvailableDestinations;
        var totalCount = allDestinations.Count;
        var availableCount = availableDestinations?.Count ?? 0;

        if (availableCount == totalCount)
        {
            return ("Healthy", null);
        }

        if (availableCount > 0)
        {
            // 一部 Destination が Unhealthy だが利用可能なものが残存
            logger.LogWarning(
                "クラスター {ClusterId}: {Available}/{Total} destinations available",
                clusterId, availableCount, totalCount);
            return ("Degraded", $"{availableCount}/{totalCount} destinations available");
        }

        // 全 Destination が利用不可
        logger.LogWarning(
            "クラスター {ClusterId}: all {Total} destinations unavailable",
            clusterId, totalCount);
        return ("Unhealthy", "All destinations unavailable");
    }
}
