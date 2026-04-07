using MailSendService.Exceptions;
using MailSendService.Services.Interfaces;

namespace MailSendService.Services;

/// <summary>
/// UserManagementService から HTTP 通信でユーザー情報を取得するリゾルバ。
/// 通信障害・タイムアウト時は null を返すフォールバック戦略を採用する。
/// </summary>
/// <remarks>
/// リクエストには分散トレーシング用の Correlation ID ヘッダー（X-Correlation-Id）を自動付与する。
/// </remarks>
public class UserInfoResolver(
    HttpClient httpClient,
    ILogger<UserInfoResolver> logger) : IUserInfoResolver
{
    /// <summary>
    /// 指定された顧客 ID でユーザー情報を UserManagementService から取得する。
    /// </summary>
    /// <param name="customerId">取得対象の顧客 ID。</param>
    /// <param name="correlationId">分散トレーシング用の相関 ID（省略時は Activity.Current?.Id を使用）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ユーザー情報。ユーザーが見つからない場合（404）、通信障害、タイムアウト時は null を返す。</returns>
    /// <remarks>
    /// <para>HTTP GET /api/v1/users/{customerId} を呼び出す。</para>
    /// <para>明示的に渡された correlationId を優先し、未指定の場合は現在のアクティビティの Trace ID を X-Correlation-Id ヘッダーとして伝搬する。</para>
    /// <para>外部サービス障害時はログを出力し null を返すことで、呼び出し元にフォールバック判断を委ねる。</para>
    /// </remarks>
    public async Task<UserInfo?> ResolveAsync(string customerId, string? correlationId = null, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{customerId}");

            var effectiveCorrelationId = correlationId ?? System.Diagnostics.Activity.Current?.Id;
            if (effectiveCorrelationId is not null)
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", effectiveCorrelationId);

            var response = await httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("ユーザーが見つかりません: {CustomerId}", customerId);
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserInfo>(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "UserManagementService 通信障害: {StatusCode}, CustomerId: {CustomerId}. フォールバックを使用します",
                ex.StatusCode, customerId);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "UserManagementService タイムアウト: CustomerId: {CustomerId}. フォールバックを使用します", customerId);
            return null;
        }
    }
}
