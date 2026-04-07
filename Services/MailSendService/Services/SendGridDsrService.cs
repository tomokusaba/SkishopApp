using System.Net.Http.Json;
using MailSendService.Services.Interfaces;

namespace MailSendService.Services;

/// <summary>
/// SendGrid API を使用した GDPR DSR コンタクト削除サービス。
/// </summary>
/// <remarks>
/// <para>spec.md §外部プロセッサーへの DSR 伝搬に基づき、
/// SendGrid Marketing Contacts API (<c>DELETE /v3/marketing/contacts</c>) を呼び出す。</para>
/// <para>DSR 受理後 24 時間以内に API 呼び出しを完了する必要がある。</para>
/// <para><see cref="IHttpClientFactory"/> 経由で HttpClient を取得し、Polly リトライポリシーを適用する。</para>
/// </remarks>
/// <param name="httpClientFactory">HttpClient ファクトリ。</param>
/// <param name="logger">ロガー。</param>
public class SendGridDsrService(
    IHttpClientFactory httpClientFactory,
    ILogger<SendGridDsrService> logger) : ISendGridDsrService
{
    /// <inheritdoc />
    public async Task DeleteContactAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("SendGrid");

            // Step 1: メールアドレスからコンタクト ID を検索
            var searchResponse = await client.PostAsJsonAsync(
                "/v3/marketing/contacts/search/emails",
                new { emails = new[] { email } }, ct);

            if (!searchResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "SendGrid contact search failed: {StatusCode}, Email={MaskedEmail}",
                    searchResponse.StatusCode, MaskEmail(email));
                return;
            }

            var searchResult = await searchResponse.Content.ReadFromJsonAsync<SendGridSearchResult>(ct);
            var contactId = searchResult?.Result?.GetValueOrDefault(email.ToLowerInvariant())?.Contact?.Id;

            if (string.IsNullOrEmpty(contactId))
            {
                logger.LogInformation("SendGrid contact not found: {MaskedEmail}", MaskEmail(email));
                return;
            }

            // Step 2: コンタクト ID で削除
            var deleteResponse = await client.DeleteAsync(
                $"/v3/marketing/contacts?ids={contactId}", ct);

            if (deleteResponse.IsSuccessStatusCode)
            {
                logger.LogInformation("SendGrid contact deleted: {MaskedEmail}", MaskEmail(email));
            }
            else
            {
                logger.LogWarning(
                    "SendGrid contact deletion failed: {StatusCode}, Email={MaskedEmail}",
                    deleteResponse.StatusCode, MaskEmail(email));
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "SendGrid API communication error: {Message}", ex.Message);
        }
    }

    /// <summary>メールアドレスの PII マスキング。</summary>
    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex <= 0 ? "***" : $"{email[0]}***{email[atIndex..]}";
    }

    /// <summary>SendGrid コンタクト検索レスポンスのデシリアライゼーション型。</summary>
    private record SendGridSearchResult(Dictionary<string, SendGridContactWrapper>? Result);
    /// <summary>SendGrid コンタクトラッパー。</summary>
    private record SendGridContactWrapper(SendGridContact? Contact);
    /// <summary>SendGrid コンタクト情報。</summary>
    private record SendGridContact(string? Id);
}
