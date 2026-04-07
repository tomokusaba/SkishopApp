using System.ComponentModel;
using AiSupportService.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace AiSupportService.Infrastructure.SemanticKernel.Plugins;

/// <summary>
/// Semantic Kernel の注文情報プラグイン。
/// </summary>
/// <remarks>
/// <para>
/// AI アシスタントがログインユーザーの注文状況を照会するための Kernel Function を提供する。
/// SalesManagementService との連携により、注文情報を取得する。
/// </para>
/// <para>
/// <b>セキュリティ（IDOR 防止）:</b>
/// OWASP Top 10 A01 対策として、全ての注文照会でログインユーザー ID との照合を行い、
/// 他ユーザーの注文情報にアクセスできないようにする。
/// <see cref="IHttpContextAccessor"/> から ClaimsPrincipal を取得し、
/// NameIdentifier クレームでユーザー ID を特定する。
/// </para>
/// <para>
/// <b>使用シナリオ:</b>
/// <list type="bullet">
///   <item><description>「注文 ORD-12345 の状況を教えて」→ 指定注文の状況を照会</description></item>
///   <item><description>「最近の注文を見せて」→ 直近の注文一覧を取得</description></item>
///   <item><description>「注文はいつ届きますか」→ 配送状況を確認</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Kernel へのプラグイン登録
/// kernel.Plugins.AddFromObject(new OrderPlugin(orderClient, httpContextAccessor));
/// </code>
/// </example>
/// <param name="orderClient">注文情報取得用のクライアント（SalesManagementService 連携）。</param>
/// <param name="httpContextAccessor">現在の HTTP コンテキストへのアクセサ。ユーザー認証情報の取得に使用。</param>
public class OrderPlugin(IOrderClient orderClient, IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// 注文 ID を指定して注文状況を取得する。ログインユーザーの注文のみ参照可能。
    /// </summary>
    /// <param name="orderId">照会対象の注文 ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>
    /// 注文情報の JSON 文字列。
    /// 未ログイン時は認証要求メッセージ、注文未検出時はエラーメッセージを返す。
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>IDOR 防止:</b> <see cref="IOrderClient.GetOrderByIdAsync"/> に
    /// ログインユーザー ID を渡し、サービス側でオーナーシップを検証する。
    /// 他ユーザーの注文 ID を指定しても、該当なしとして返される。
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// {
    ///   "OrderId": "ORD-12345",
    ///   "Status": "SHIPPED",
    ///   "TrackingNumber": "1234567890",
    ///   "Items": [...],
    ///   "TotalAmount": 15000
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("get_order_status")]
    [Description("注文IDを指定して注文状況を確認します。ログインユーザーの注文のみ取得可能です。")]
    public async Task<string> GetOrderStatusAsync(
        [Description("注文ID")] string orderId,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return "注文状況を確認するにはログインが必要です。";

        var order = await orderClient.GetOrderByIdAsync(orderId, userId, ct);
        return order is not null
            ? System.Text.Json.JsonSerializer.Serialize(order)
            : "指定された注文が見つかりませんでした。注文IDをご確認ください。";
    }

    /// <summary>
    /// ログインユーザーの最近の注文一覧を取得する。
    /// </summary>
    /// <param name="count">取得件数（デフォルト: 5、最大推奨: 10）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>
    /// 注文一覧の JSON 文字列。
    /// 未ログイン時は認証要求メッセージを返す。
    /// </returns>
    /// <remarks>
    /// <para>
    /// AI モデルは「最近の注文を見せて」「注文履歴を確認したい」といったユーザー発話に対して
    /// この関数を呼び出す。件数パラメータは AI が文脈から判断して設定する。
    /// </para>
    /// <para>
    /// <b>IDOR 防止:</b> ログインユーザーの注文のみが返される。
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// [
    ///   { "OrderId": "ORD-12345", "Status": "DELIVERED", "TotalAmount": 15000, "OrderedAt": "2026-01-15" },
    ///   { "OrderId": "ORD-12344", "Status": "SHIPPED", "TotalAmount": 8000, "OrderedAt": "2026-01-10" }
    /// ]
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("get_recent_orders")]
    [Description("ログインユーザーの最近の注文一覧を取得します。")]
    public async Task<string> GetRecentOrdersAsync(
        [Description("取得件数（デフォルト: 5）")] int count = 5,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return "注文履歴を確認するにはログインが必要です。";

        var orders = await orderClient.GetRecentOrdersAsync(userId, count, ct);
        return System.Text.Json.JsonSerializer.Serialize(orders);
    }

    /// <summary>
    /// HttpContext からログインユーザーの ID を取得する。
    /// </summary>
    /// <returns>
    /// ユーザー ID（<see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> クレームの値）。
    /// 未ログイン時は <c>null</c>。
    /// </returns>
    /// <remarks>
    /// JWT トークンまたは Cookie 認証から ClaimsPrincipal が設定されている前提。
    /// 認証スキームによっては異なるクレームタイプを使用する場合があるため、
    /// 必要に応じて調整すること。
    /// </remarks>
    private string? GetCurrentUserId()
        => httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
