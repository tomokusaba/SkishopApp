using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiSupportService.Infrastructure.HealthChecks;

/// <summary>
/// Azure OpenAI サービスの疎通を確認するヘルスチェック実装。
/// </summary>
/// <remarks>
/// <para>
/// このヘルスチェックは Kubernetes や .NET Aspire の Readiness プローブとして使用され、
/// Azure OpenAI への接続状態を検証する。AI チャット機能が利用可能かどうかを判定する。
/// </para>
/// <para>
/// 検証方法: Semantic Kernel の <see cref="IChatCompletionService"/> を使用して "ping" メッセージを送信し、
/// レスポンスが返ることで接続状態を判定する。タイムアウト（10 秒）を設定し、長時間のブロッキングを防止する。
/// </para>
/// <para>
/// <b>依存サービス:</b>
/// <list type="bullet">
///   <item><description><see cref="Kernel"/> - Semantic Kernel インスタンス（Azure OpenAI 構成済み）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>コスト考慮:</b> ヘルスチェックの頻度によってはトークン消費が発生するため、
/// 過度に短い間隔での実行は避けること。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs でのヘルスチェック登録
/// builder.Services.AddHealthChecks()
///     .AddCheck&lt;AzureOpenAIHealthCheck&gt;("azure-openai", tags: ["ready"]);
/// </code>
/// </example>
/// <param name="kernel">Azure OpenAI が構成された Semantic Kernel インスタンス。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class AzureOpenAIHealthCheck(Kernel kernel, ILogger<AzureOpenAIHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// Azure OpenAI に対して "ping" メッセージを送信し、レスポンスが返ることで接続状態を検証する。
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
    ///   <item><description><see cref="HealthCheckResult.Healthy"/> - チャット応答が正常に返った場合</description></item>
    ///   <item><description><see cref="HealthCheckResult.Unhealthy"/> - タイムアウトまたは例外発生時</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// "ping" という最小限のメッセージを送信するため、トークン消費は最小限に抑えられる。
    /// レスポンス内容は検証せず、応答が返ることのみを確認する。
    /// </para>
    /// <para>
    /// <b>セキュリティ:</b> ヘルスチェック用のメッセージはシステムプロンプトを含まないため、
    /// AI アシスタントとしての振る舞いは適用されない。
    /// </para>
    /// </remarks>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddUserMessage("ping");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            await chatService.GetChatMessageContentsAsync(history, cancellationToken: cts.Token);
            return HealthCheckResult.Healthy("Azure OpenAI に接続可能");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure OpenAI ヘルスチェック失敗");
            return HealthCheckResult.Unhealthy("Azure OpenAI に接続不可", ex);
        }
    }
}
