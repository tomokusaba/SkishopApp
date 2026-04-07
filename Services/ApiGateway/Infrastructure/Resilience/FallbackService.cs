// ─────────────────────────────────────────────────────────────
// FallbackService — サーキットブレーカー Open 時のフォールバック戦略
//
// 設計書 §7 に基づき、各クラスターの障害時に代替レスポンスを提供する。
// Redis キャッシュ応答、デフォルト値応答、静的リスト応答を実装する。
// ─────────────────────────────────────────────────────────────

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace ApiGateway.Infrastructure.Resilience;

/// <summary>
/// サーキットブレーカー Open 時のフォールバックレスポンスを生成するサービス。
/// <para>設計書 §7 フォールバック戦略:</para>
/// <list type="bullet">
///   <item><description>AuthService → 503 即時返却（認証は代替不可）</description></item>
///   <item><description>InventoryManagementService → Redis キャッシュ応答</description></item>
///   <item><description>SalesManagementService → 503 即時返却（注文データは代替不可）</description></item>
///   <item><description>PaymentCartService → 503 即時返却（決済は安全性優先）</description></item>
///   <item><description>CouponService → デフォルト値（クーポンなし）</description></item>
///   <item><description>PointService → デフォルト値（0 ポイント）</description></item>
///   <item><description>AiSupportService → 人気商品の静的リスト</description></item>
///   <item><description>UserManagementService → 503 即時返却</description></item>
/// </list>
/// </summary>
public sealed class FallbackService(
    IDistributedCache cache,
    ILogger<FallbackService> logger) : IFallbackService
{
    /// <summary>フォールバックキャッシュの TTL（10 分）。</summary>
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    /// <inheritdoc/>
    public async Task<FallbackResponse?> GetFallbackAsync(
        string clusterId, string requestPath, CancellationToken ct = default)
    {
        return clusterId switch
        {
            "inventory-cluster" => await GetInventoryFallbackAsync(requestPath, ct),
            "coupons-cluster" => GetCouponFallback(),
            "points-cluster" => GetPointsFallback(),
            "ai-cluster" => GetAiFallback(requestPath),
            // auth / user / sales / payment-cart → 503 即時返却（代替不可）
            _ => null
        };
    }

    /// <inheritdoc/>
    public async Task CacheResponseAsync(
        string clusterId, string requestPath, string responseBody, CancellationToken ct = default)
    {
        if (clusterId != "inventory-cluster" || string.IsNullOrEmpty(responseBody))
            return;

        var cacheKey = SanitizeCacheKey(requestPath);
        await cache.SetStringAsync(cacheKey, responseBody, CacheOptions, ct);
        logger.LogDebug(
            "フォールバックキャッシュ書込み: {ClusterId}, Path={Path}", clusterId, requestPath);
    }

    /// <summary>在庫サービスのフォールバック: Redis キャッシュから最新の商品データを返却。</summary>
    private async Task<FallbackResponse?> GetInventoryFallbackAsync(
        string requestPath, CancellationToken ct)
    {
        var cacheKey = SanitizeCacheKey(requestPath);
        var cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            logger.LogInformation(
                "フォールバック応答（Redis キャッシュ）: {ClusterId}, Path={Path}",
                "inventory-cluster", requestPath);
            return new FallbackResponse(200, cached, "application/json");
        }

        return null; // キャッシュなし → 503
    }

    /// <summary>クーポンサービスのフォールバック: クーポンなしのデフォルト値を返却。</summary>
    private static FallbackResponse GetCouponFallback()
        => new(200, """{"coupons":[],"message":"クーポン情報は現在取得できません"}""", "application/json");

    /// <summary>ポイントサービスのフォールバック: 0 ポイントのデフォルト値を返却。</summary>
    private static FallbackResponse GetPointsFallback()
        => new(200, """{"balance":0,"message":"ポイント情報は現在取得できません"}""", "application/json");

    /// <summary>AI サービスのフォールバック: 人気商品の静的リストを返却。</summary>
    private static FallbackResponse? GetAiFallback(string requestPath)
    {
        if (requestPath.Contains("recommendations", StringComparison.OrdinalIgnoreCase))
        {
            return new FallbackResponse(200,
                """{"recommendations":[{"id":"popular-1","name":"人気スキー板 A"},{"id":"popular-2","name":"人気ブーツ B"},{"id":"popular-3","name":"人気ウェア C"}],"source":"fallback"}""",
                "application/json");
        }

        return null; // search / chat / analytics → フォールバック不可
    }

    /// <summary>
    /// リクエストパスをハッシュ化してキャッシュキーインジェクションを防止する。
    /// </summary>
    private static string SanitizeCacheKey(string requestPath)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(requestPath)));
        return $"fallback:inventory:{hash}";
    }
}

/// <summary>フォールバックレスポンスの内容を表すレコード。</summary>
/// <param name="StatusCode">HTTP ステータスコード。</param>
/// <param name="Body">レスポンスボディ（JSON 文字列）。</param>
/// <param name="ContentType">Content-Type ヘッダー値。</param>
public record FallbackResponse(int StatusCode, string Body, string ContentType);
