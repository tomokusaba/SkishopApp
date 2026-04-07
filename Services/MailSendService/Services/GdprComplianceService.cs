using System.Security.Cryptography;
using System.Text;
using MailSendService.Exceptions;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace MailSendService.Services;

/// <summary>
/// GDPR（EU 一般データ保護規則）準拠の操作を提供するサービス実装。
/// </summary>
/// <remarks>
/// <para>P2-2: MailService からの責務分割。</para>
/// <para>以下の GDPR 権利に対応する:</para>
/// <list type="bullet">
/// <item><description>第 17 条: 消去権（忘れられる権利）</description></item>
/// <item><description>第 18 条: 処理の制限を求める権利</description></item>
/// <item><description>第 21 条: 異議を唱える権利（ダイレクトマーケティングの拒否）</description></item>
/// </list>
/// </remarks>
public class GdprComplianceService(
    IMailLogRepository mailLogRepository,
    IMailSuppressionRepository suppressionRepository,
    IOutboxEventRepository outboxEventRepository,
    IDistributedCache cache,
    ILogger<GdprComplianceService> logger) : IGdprComplianceService
{
    /// <summary>抑制キャッシュキーのプレフィックス。</summary>
    private const string SuppressionCacheKeyPrefix = "mail:suppression:";

    /// <inheritdoc />
    /// <remarks>
    /// consentType が "marketing" 以外の場合は何もしない。
    /// 既に抑制レコードが存在する場合は重複登録しない。
    /// </remarks>
    public async Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default)
    {
        if (consentType is not "marketing")
            return;

        var email = await mailLogRepository.FindEmailByUserIdAsync(userId, ct);
        if (email is null) return;

        var existing = await suppressionRepository.FindByEmailAndReasonAsync(email, SuppressionReason.Unsubscribe, ct);
        if (existing is not null) return;

        await suppressionRepository.AddAsync(new MailSuppression
        {
            Email = email,
            Reason = SuppressionReason.Unsubscribe
        }, ct);
        await suppressionRepository.SaveChangesAsync(ct);
        await InvalidateSuppressionCacheAsync(email, ct);
        logger.LogInformation("Marketing suppression added for consent revocation: UserId={UserId}", userId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>メールアドレスを SHA-256 ハッシュベースの匿名アドレスに置換し、氏名・ユーザー ID を null に設定する。</para>
    /// <para>未送信（Pending）のメールは Skipped ステータスに変更する。</para>
    /// <para>元のメールアドレスに紐づく抑制レコードも削除する。</para>
    /// </remarks>
    public async Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default)
    {
        var logs = await mailLogRepository.FindByRecipientUserIdAsync(userId, limit: 1000, ct);
        if (logs.Count == 0) return;

        await using var transaction = await outboxEventRepository.BeginTransactionAsync(ct);
        try
        {
            var hash = ComputeSha256Hash(userId);
            var anonymizedEmail = $"{hash}@anonymized.local";
            var originalEmail = logs.FirstOrDefault()?.RecipientEmail;

            foreach (var log in logs)
            {
                // DDD: Pending のメールをスキップ
                if (log.Status == MailLogStatus.Pending)
                {
                    log.MarkAsSkipped("User deleted");
                }
                // DDD: PII を匿名化
                log.AnonymizePii(anonymizedEmail);
            }

            if (originalEmail is not null && originalEmail != anonymizedEmail)
            {
                var suppressions = await suppressionRepository.FindByUserEmailAsync(originalEmail, ct);
                foreach (var suppression in suppressions)
                {
                    await suppressionRepository.RemoveAsync(suppression, ct);
                }
            }

            try
            {
                await mailLogRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex, "Concurrency conflict anonymizing user: {UserId}", userId);
                throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
            }

            await outboxEventRepository.CommitTransactionAsync(transaction, ct);
            logger.LogInformation("PII anonymized for deleted user: UserId={UserId}, Records={Count}",
                userId, logs.Count);
        }
        catch
        {
            await outboxEventRepository.RollbackTransactionAsync(transaction, ct);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 既に抑制レコードが存在する場合は重複登録しない。抑制キャッシュを無効化する。
    /// </remarks>
    public async Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default)
    {
        var email = await mailLogRepository.FindEmailByUserIdAsync(userId, ct);
        if (email is null) return;

        var existing = await suppressionRepository.FindByEmailAndReasonAsync(email, SuppressionReason.ProcessingRestricted, ct);
        if (existing is not null) return;

        await suppressionRepository.AddAsync(new MailSuppression
        {
            Email = email,
            Reason = SuppressionReason.ProcessingRestricted
        }, ct);
        await suppressionRepository.SaveChangesAsync(ct);
        await InvalidateSuppressionCacheAsync(email, ct);
        logger.LogInformation("Suppression added for processing restriction: UserId={UserId}", userId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 抑制理由 <c>ProcessingRestricted</c> のレコードを削除し、抑制キャッシュを無効化する。
    /// </remarks>
    public async Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default)
    {
        var email = await mailLogRepository.FindEmailByUserIdAsync(userId, ct);
        if (email is null) return;

        var suppression = await suppressionRepository.FindByEmailAndReasonAsync(email, SuppressionReason.ProcessingRestricted, ct);
        if (suppression is not null)
        {
            await suppressionRepository.RemoveAsync(suppression, ct);
            await suppressionRepository.SaveChangesAsync(ct);
            await InvalidateSuppressionCacheAsync(email, ct);
            logger.LogInformation("Suppression removed for processing unrestriction: UserId={UserId}", userId);
        }
    }

    /// <summary>
    /// 抑制キャッシュを無効化する。
    /// </summary>
    private async Task InvalidateSuppressionCacheAsync(string email, CancellationToken ct)
    {
        await cache.RemoveAsync($"{SuppressionCacheKeyPrefix}{email}", ct);
    }

    /// <summary>
    /// SHA-256 ハッシュを計算する。
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
