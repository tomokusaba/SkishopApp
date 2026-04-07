// ─────────────────────────────────────────────────────────────
// CircuitBreakerMiddleware — YARP 転送前のサーキットブレーカーチェック
//
// YARP のリクエスト転送前にクラスター別サーキット状態を確認し、
// Open 状態の場合はフォールバックレスポンスまたは 503 を返す。
// YARP 転送後のレスポンスステータスに基づいて成功/失敗を記録する。
// ─────────────────────────────────────────────────────────────

using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Yarp.ReverseProxy;

namespace ApiGateway.Infrastructure.Resilience;

/// <summary>
/// YARP リバースプロキシの前段で動作するサーキットブレーカーミドルウェア。
/// <para>
/// リクエストパスからクラスター ID を解決し、<see cref="ICircuitBreakerService"/> で
/// サーキット状態を確認する。Open 状態の場合は <see cref="IFallbackService"/> で
/// フォールバックレスポンスを生成するか、RFC 9457 Problem Details 形式の 503 を返す。
/// </para>
/// </summary>
public sealed class CircuitBreakerMiddleware(
    RequestDelegate next,
    ICircuitBreakerService circuitBreaker,
    IFallbackService fallbackService,
    ILogger<CircuitBreakerMiddleware> logger)
{
    /// <summary>リクエストパスプレフィックスとクラスター ID のマッピング。</summary>
    private static readonly (string PathPrefix, string ClusterId)[] PathToClusterMappings =
    [
        // AuthService: /api/v1/auth/*
        ("/api/v1/auth/", "auth-cluster"),
        // UserManagementService: /api/v1/users/*, /api/v1/admin/users/*
        ("/api/v1/users/", "user-cluster"),
        ("/api/v1/admin/users/", "user-cluster"),
        // InventoryManagementService: /api/products/*, /api/categories/*, etc. (no v1)
        ("/api/products/", "inventory-cluster"),
        ("/api/categories/", "inventory-cluster"),
        ("/api/inventory/", "inventory-cluster"),
        ("/api/prices/", "inventory-cluster"),
        ("/api/reviews/", "inventory-cluster"),
        ("/api/size-guides/", "inventory-cluster"),
        // SalesManagementService: /api/v1/orders/*, /api/v1/shipments/*, etc.
        ("/api/v1/orders/", "sales-cluster"),
        ("/api/v1/shipments/", "sales-cluster"),
        ("/api/v1/returns/", "sales-cluster"),
        ("/api/v1/reports/", "sales-cluster"),
        // PaymentCartService: /api/v1/cart/*, /api/v1/payments/*
        ("/api/v1/cart/", "payment-cart-cluster"),
        ("/api/v1/payments/", "payment-cart-cluster"),
        // PointService: /api/v1/points/*, /api/v1/admin/points/*
        ("/api/v1/points/", "points-cluster"),
        ("/api/v1/admin/points/", "points-cluster"),
        // CouponService: /api/v1/coupons/*, /api/v1/admin/campaigns/*
        ("/api/v1/coupons/", "coupons-cluster"),
        ("/api/v1/admin/campaigns/", "coupons-cluster"),
        // AiSupportService: /api/v1/ai/*
        ("/api/v1/ai/", "ai-cluster"),
        // MailSendService: /admin/mail/*
        ("/admin/mail/", "mail-cluster")
    ];

    /// <summary>
    /// リクエストのクラスター ID を解決し、サーキット状態をチェックする。
    /// Open の場合はフォールバックまたは 503 を返し、Closed/HalfOpen の場合は
    /// YARP に転送して結果に基づいて成功/失敗を記録する。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var clusterId = ResolveClusterId(path);

        // API パス以外（/health 等）はサーキットブレーカーをスキップ
        if (clusterId is null)
        {
            await next(context);
            return;
        }

        // サーキットが Open → フォールバックまたは 503
        if (!circuitBreaker.AllowRequest(clusterId))
        {
            logger.LogWarning(
                "サーキットブレーカー Open — リクエスト拒否: {ClusterId}, Path={Path}",
                clusterId, path);

            var fallback = await fallbackService.GetFallbackAsync(clusterId, path, context.RequestAborted);
            if (fallback is not null)
            {
                context.Response.StatusCode = fallback.StatusCode;
                context.Response.ContentType = fallback.ContentType;
                await context.Response.WriteAsync(fallback.Body, context.RequestAborted);
                return;
            }

            // フォールバック不可 → 503 + GW-5002
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6",
                title = ReasonPhrases.GetReasonPhrase(503),
                status = 503,
                detail = "バックエンドサービスが一時的に利用できません（サーキットブレーカー発動中）",
                instance = path,
                code = "GW-5002",
                traceId = Activity.Current?.Id ?? context.TraceIdentifier
            }, context.RequestAborted);
            return;
        }

        // YARP に転送
        await next(context);

        // 転送結果に基づいてサーキットブレーカーを更新
        var statusCode = context.Response.StatusCode;
        if (statusCode >= 500)
        {
            circuitBreaker.RecordFailure(clusterId);
        }
        else
        {
            circuitBreaker.RecordSuccess(clusterId);
        }
    }

    /// <summary>リクエストパスからクラスター ID を解決する。</summary>
    private static string? ResolveClusterId(string path)
    {
        foreach (var (prefix, clusterId) in PathToClusterMappings)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return clusterId;
        }
        return null;
    }
}
