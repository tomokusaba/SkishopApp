using Azure;
using Azure.Communication.Email;
using MailSendService.Configurations;
using MailSendService.Exceptions;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MailSendService.Services;

/// <summary>
/// Azure Communication Services (ACS) を使用してメールを送信するサービス。
/// 送信タイムアウト 30 秒を設定し、タイムアウト・ACS エラーをハンドリングする。
/// </summary>
/// <remarks>
/// <para>送信操作は <see cref="CancellationTokenSource.CreateLinkedTokenSource"/> で
/// 呼び出し元のキャンセルトークンと 30 秒タイムアウトを統合する。</para>
/// <para>ログ出力時のメールアドレスは PII 保護のためマスキングされる。</para>
/// </remarks>
public class AzureEmailSender(
    EmailClient emailClient,
    IOptions<AzureEmailSettings> settings,
    ILogger<AzureEmailSender> logger) : IAzureEmailSender
{
    /// <summary>
    /// Azure Communication Services 経由でメールを送信する。
    /// </summary>
    /// <param name="recipientEmail">宛先メールアドレス。</param>
    /// <param name="recipientName">宛先の表示名。</param>
    /// <param name="subject">メール件名。</param>
    /// <param name="htmlBody">HTML 形式の本文。</param>
    /// <param name="plainTextBody">プレーンテキスト形式の本文（任意）。</param>
    /// <param name="attachments">添付ファイル情報のリスト（任意）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>Azure ACS の操作 ID。</returns>
    /// <exception cref="MailSendFailedException">送信タイムアウト（30 秒）または Azure ACS エラーが発生した場合。</exception>
    public async Task<string> SendAsync(
        string recipientEmail, string recipientName,
        string subject, string htmlBody, string? plainTextBody = null,
        IReadOnlyList<EmailAttachmentInfo>? attachments = null,
        CancellationToken ct = default)
    {
        var senderAddress = settings.Value.SenderAddress;

        var emailMessage = new EmailMessage(
            senderAddress: senderAddress,
            recipientAddress: recipientEmail,
            content: new EmailContent(subject)
            {
                Html = htmlBody,
                PlainText = plainTextBody
            });

        try
        {
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            sendCts.CancelAfter(TimeSpan.FromSeconds(30));

            var operation = await emailClient.SendAsync(
                WaitUntil.Completed, emailMessage, sendCts.Token);

            logger.LogInformation("Email sent: Recipient={MaskedEmail}, OperationId={OperationId}",
                MaskEmail(recipientEmail), operation.Id);

            return operation.Id;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Azure ACS send timeout (30s): Recipient={MaskedEmail}",
                MaskEmail(recipientEmail));
            throw new MailSendFailedException("Azure ACS 送信タイムアウト (30秒)", ex);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure ACS send failed: Status={Status}, Code={ErrorCode}",
                ex.Status, ex.ErrorCode);
            throw new MailSendFailedException($"Azure ACS エラー: {ex.ErrorCode}", ex);
        }
    }

    /// <summary>
    /// メールアドレスを PII 保護のためマスキングする（例: "t***@example.com"）。
    /// @ の前が 1 文字以下の場合は "***@***" を返す。
    /// </summary>
    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***";
        return $"{email[0]}***@{email[(atIndex + 1)..]}";
    }
}
