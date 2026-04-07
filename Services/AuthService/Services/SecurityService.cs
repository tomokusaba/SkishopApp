using AuthService.Configurations;
using AuthService.Exceptions;
using AuthService.Infrastructure.Metrics;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

/// <summary>
/// <see cref="ISecurityService"/> の実装クラス。
/// セキュリティイベントのログ記録、ログイン試行管理、アカウントロック機能を提供します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>ブルートフォース攻撃対策: 設定回数の失敗後にアカウントを自動ロック</item>
///   <item>自動アンロック: 設定時間経過後にアカウントを自動解除</item>
///   <item>セキュリティイベントログ: すべてのセキュリティ関連操作を記録</item>
///   <item>メトリクス: アカウントロックをメトリクスとして記録</item>
/// </list>
/// </remarks>
/// <param name="securityLogRepository">セキュリティログリポジトリ。</param>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="authSettings">認証設定。</param>
/// <param name="logger">ロガー。</param>
public class SecurityService(
    ISecurityLogRepository securityLogRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider,
    IOptions<AuthSettings> authSettings,
    ILogger<SecurityService> logger) : ISecurityService
{
    private readonly AuthSettings _authSettings = authSettings.Value;

    /// <inheritdoc />
    public async Task LogSecurityEventAsync(
        string? userId,
        string eventType,
        string? ipAddress,
        string? userAgent,
        string? details,
        CancellationToken ct = default,
        bool saveImmediately = true)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var log = new SecurityLog
        {
            UserId = userId,
            EventType = eventType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details,
            IsSuccess = !eventType.Contains("FAILED", StringComparison.OrdinalIgnoreCase)
                && !eventType.Contains("ATTACK", StringComparison.OrdinalIgnoreCase)
                && !eventType.Contains("LOCKED", StringComparison.OrdinalIgnoreCase)
        };

        await securityLogRepository.AddAsync(log, ct);
        if (saveImmediately)
            await securityLogRepository.SaveChangesAsync(ct);

        logger.LogInformation("セキュリティイベント記録: EventType={EventType}, UserId={UserId}", eventType, userId);
    }

    /// <inheritdoc />
    public async Task<bool> IncrementFailedAttemptsAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is null)
        {
            logger.LogWarning("FailedAttempts 更新対象ユーザーが見つかりません: UserId={UserId}", userId);
            return false;
        }

        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= _authSettings.MaxFailedAttempts)
        {
            user.IsAccountLocked = true;
            user.LockedAt = timeProvider.GetUtcNow();

            try
            {
                await userRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ex.Entries.FirstOrDefault()?.Entity.GetType().Name);
                throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
            }

            AuthMetrics.AccountLockouts.Add(1);

            logger.LogWarning("アカウントロック: UserId={UserId}, FailedAttempts={FailedAttempts}",
                userId, user.FailedLoginAttempts);

            await LogSecurityEventAsync(
                userId, "ACCOUNT_LOCKED", null, null,
                $"ログイン試行回数超過: {user.FailedLoginAttempts}回", ct);

            return true;
        }

        await userRepository.SaveChangesAsync(ct);
        return false;
    }

    /// <inheritdoc />
    public async Task ResetFailedAttemptsAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is null) return;

        user.FailedLoginAttempts = 0;
        await userRepository.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsAccountLockedAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is null) return false;

        if (!user.IsAccountLocked) return false;

        var now = timeProvider.GetUtcNow();
        if (user.LockedAt.HasValue &&
            user.LockedAt.Value.AddMinutes(_authSettings.AutoUnlockMinutes) < now)
        {
            await UnlockAccountAsync(userId, ct);
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task UnlockAccountAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user is null) return;

        user.IsAccountLocked = false;
        user.FailedLoginAttempts = 0;
        user.LockedAt = null;
        await userRepository.SaveChangesAsync(ct);

        await LogSecurityEventAsync(
            userId, "ACCOUNT_UNLOCKED", null, null, "アカウントがアンロックされました", ct);

        logger.LogInformation("アカウントアンロック: UserId={UserId}", userId);
    }
}
