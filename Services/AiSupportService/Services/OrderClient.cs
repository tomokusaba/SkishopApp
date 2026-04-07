using System.Net.Http.Json;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IOrderClient"/> の実装。SalesManagementService の REST API に HTTP リクエストを送信する。
/// </summary>
/// <remarks>
/// <para>
/// このクライアントは、AI チャットボットやレコメンデーションエンジンが
/// ユーザーの注文情報にアクセスするために使用します。
/// </para>
/// <para>
/// 耐障害性:
/// <list type="bullet">
///   <item><description><see cref="HttpClient"/> は <see cref="System.Net.Http.IHttpClientFactory"/> で管理</description></item>
///   <item><description>HTTP エラー発生時は例外をスローせず、<c>null</c> または空のリストを返却</description></item>
///   <item><description>エラーはログに記録し、呼び出し元でのフォールバック処理を可能に</description></item>
/// </list>
/// </para>
/// <para>
/// このデザインにより、SalesManagementService の障害が AI 機能全体の
/// 停止につながることを防ぎます。
/// </para>
/// </remarks>
/// <param name="httpClient">HTTP クライアント（DI で注入される型付きクライアント）。</param>
/// <param name="logger">ロガー。</param>
public class OrderClient(HttpClient httpClient, ILogger<OrderClient> logger) : IOrderClient
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// API エンドポイント: <c>GET /api/v1/orders/{orderId}?userId={userId}</c>
    /// </para>
    /// <para>
    /// エラー処理:
    /// <see cref="HttpRequestException"/> が発生した場合、エラーをログに記録し <c>null</c> を返します。
    /// これにより、API エラーが呼び出し元の処理を中断させることを防ぎます。
    /// </para>
    /// </remarks>
    public async Task<OrderInfo?> GetOrderByIdAsync(string orderId, string userId, CancellationToken ct = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<OrderInfo>(
                $"/api/v1/orders/{orderId}?userId={Uri.EscapeDataString(userId)}", ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "注文詳細取得失敗: OrderId={OrderId}", orderId);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// API エンドポイント: <c>GET /api/v1/orders?userId={userId}&amp;count={count}</c>
    /// </para>
    /// <para>
    /// エラー処理:
    /// <see cref="HttpRequestException"/> が発生した場合、エラーをログに記録し空のリストを返します。
    /// これにより、API エラーが呼び出し元の処理を中断させることを防ぎます。
    /// </para>
    /// </remarks>
    public async Task<List<OrderSummary>> GetRecentOrdersAsync(string userId, int count = 5, CancellationToken ct = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<OrderSummary>>(
                $"/api/v1/orders?userId={Uri.EscapeDataString(userId)}&count={count}", ct);
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "注文一覧取得失敗: UserId={UserId}", userId);
            return [];
        }
    }
}
