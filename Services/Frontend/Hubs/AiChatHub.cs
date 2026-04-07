using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace Frontend.Hubs;

/// <summary>
/// AI チャットストリーミング用 SignalR Hub（§16.3 準拠）。
/// AiSupportService からの応答をクライアントに中継する。
/// IHttpClientFactory を直接使用し、Blazor 固有の依存（NavigationManager 等）を回避する。
/// </summary>
public class AiChatHub(
    IHttpClientFactory httpClientFactory,
    ILogger<AiChatHub> logger) : Hub
{
    /// <summary>
    /// クライアント接続時にコネクション ID をログ出力する。
    /// </summary>
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("AI チャット SignalR 接続: ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// クライアント切断時にコネクション ID をログ出力する。
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("AI チャット SignalR 切断: ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// クライアントからメッセージを受信し、API Gateway 経由で AiSupportService に送信。
    /// レスポンスをトークン単位でストリーミング返却する。
    /// </summary>
    public async Task SendMessage(string sessionId, string message)
    {
        var httpClient = httpClientFactory.CreateClient("AiChat");

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                $"/api/v1/ai/chat/sessions/{sessionId}/messages",
                new { message },
                CancellationToken.None);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

            var content = json.GetProperty("content").GetString() ?? string.Empty;
            var messageId = json.GetProperty("id").GetString() ?? string.Empty;

            // チャンク単位でストリーミング（UX 向上）
            const int chunkSize = 3;
            for (var i = 0; i < content.Length; i += chunkSize)
            {
                var chunk = content[i..Math.Min(i + chunkSize, content.Length)];
                await Clients.Caller.SendAsync("ReceiveToken", chunk);
                await Task.Delay(15, CancellationToken.None);
            }

            await Clients.Caller.SendAsync("MessageComplete", messageId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI チャットメッセージ送信エラー: SessionId={SessionId}", sessionId);
            await Clients.Caller.SendAsync("ReceiveToken", "申し訳ございません。応答の取得に失敗しました。");
            await Clients.Caller.SendAsync("MessageComplete", string.Empty);
        }
    }
}
