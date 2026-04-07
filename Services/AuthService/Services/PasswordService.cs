using System.Text.Json;
using AuthService.Configurations;
using AuthService.DTOs.Events;
using AuthService.Exceptions;
using AuthService.Infrastructure.Metrics;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

/// <summary>
/// <see cref="IPasswordService"/> の実装クラス。
/// パスワードリセット、パスワード変更、およびパスワード履歴管理を担当します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>パスワード履歴チェック: 直近5つのパスワードの再利用を防止</item>
///   <item>安全なトークン生成: 暗号学的に安全な乱数でリセットトークンを生成</item>
///   <item>タイミング攻撃対策: 存在しないメールアドレスでも同一レスポンスを返却</item>
///   <item>トークンの一回使用: リセットトークンは一度だけ使用可能</item>
///   <item>イベント駆動: パスワード変更イベントをOutboxに記録</item>
/// </list>
/// </remarks>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="passwordResetRepository">パスワードリセットリポジトリ。</param>
/// <param name="passwordHistoryRepository">パスワード履歴リポジトリ。</param>
/// <param name="passwordHasher">パスワードハッシャー。</param>
/// <param name="outboxEventRepository">Outboxイベントリポジトリ。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="authSettings">認証設定。</param>
/// <param name="logger">ロガー。</param>
public class PasswordService(
    IUserRepository userRepository,
    IPasswordResetRepository passwordResetRepository,
    IPasswordHistoryRepository passwordHistoryRepository,
    IPasswordHasher<User> passwordHasher,
    IOutboxEventRepository outboxEventRepository,
    ISecurityService securityService,
    TimeProvider timeProvider,
    IOptions<AuthSettings> authSettings,
    ILogger<PasswordService> logger) : IPasswordService
{
    private readonly AuthSettings _authSettings = authSettings.Value;

    /// <summary>
    /// パスワード履歴に保持するパスワードの数。
    /// </summary>
    private const int PasswordHistoryCount = 5;

    /// <inheritdoc />
    public async Task RequestResetAsync(string email, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var user = await userRepository.FindByEmailAsync(email, ct);
        if (user is null)
        {
            logger.LogWarning("パスワードリセット要求: 存在しないメールアドレス（セキュリティのため同一レスポンスを返却）");
            return;
        }

        await passwordResetRepository.InvalidateExistingTokensAsync(user.Id, "PASSWORD_RESET", ct);

        var tokenValue = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var passwordReset = new PasswordReset
        {
            UserId = user.Id,
            Token = tokenValue,
            TokenType = "PASSWORD_RESET",
            ExpiresAt = timeProvider.GetUtcNow().AddHours(24)
        };

        await passwordResetRepository.AddAsync(passwordReset, ct);

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.password_reset.requested",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(new
            {
                UserId = user.Id,
                OccurredAt = timeProvider.GetUtcNow()
            })
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        await outboxEventRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            user.Id, "PASSWORD_RESET_REQUESTED", null, null, "パスワードリセットが要求されました", ct);

        AuthMetrics.PasswordResets.Add(1);

        logger.LogInformation("パスワードリセットトークン発行: UserId={UserId}", user.Id);
    }

    /// <inheritdoc />
    public async Task ConfirmResetAsync(string token, string newPassword, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(newPassword);

        var resetToken = await passwordResetRepository.FindByTokenAsync(token, ct)
            ?? throw new BusinessException("無効なリセットトークンです");

        if (resetToken.IsUsed)
        {
            throw new BusinessException("このリセットトークンは既に使用されています");
        }

        if (resetToken.ExpiresAt < timeProvider.GetUtcNow())
        {
            throw new BusinessException("リセットトークンの有効期限が切れています");
        }

        var user = await userRepository.FindByIdAsync(resetToken.UserId, ct)
            ?? throw new NotFoundException("ユーザーが見つかりません");

        await ValidatePasswordHistoryAsync(user, newPassword, ct);

        var hashedPassword = passwordHasher.HashPassword(user, newPassword);
        user.PasswordHash = hashedPassword;

        var history = new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = hashedPassword
        };
        await passwordHistoryRepository.AddAsync(history, ct);

        resetToken.IsUsed = true;
        resetToken.UsedAt = timeProvider.GetUtcNow();

        var passwordEvent = new PasswordChangedEvent(
            Guid.NewGuid().ToString(),
            user.Id,
            "RESET",
            timeProvider.GetUtcNow());

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.password.changed",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(passwordEvent)
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            user.Id, "PASSWORD_RESET_COMPLETED", null, null, "パスワードがリセットされました", ct);

        logger.LogInformation("パスワードリセット完了: UserId={UserId}", user.Id);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        var verifyResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash ?? string.Empty, currentPassword);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            await securityService.LogSecurityEventAsync(
                userId, "PASSWORD_CHANGE_FAILED", null, null, "現在のパスワードが一致しません", ct);
            throw new BusinessException("現在のパスワードが正しくありません");
        }

        await ValidatePasswordHistoryAsync(user, newPassword, ct);

        var hashedPassword = passwordHasher.HashPassword(user, newPassword);
        user.PasswordHash = hashedPassword;

        var history = new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = hashedPassword
        };
        await passwordHistoryRepository.AddAsync(history, ct);

        var passwordEvent = new PasswordChangedEvent(
            Guid.NewGuid().ToString(),
            user.Id,
            "CHANGE",
            timeProvider.GetUtcNow());

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.password.changed",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(passwordEvent)
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            userId, "PASSWORD_CHANGED", null, null, "パスワードが変更されました", ct);

        logger.LogInformation("パスワード変更完了: UserId={UserId}", userId);
    }

    /// <summary>
    /// 新しいパスワードがパスワード履歴に存在しないことを検証します。
    /// </summary>
    /// <param name="user">検証対象のユーザー。</param>
    /// <param name="newPassword">新しいパスワード（平文）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="BusinessException">新しいパスワードが過去に使用されたものと同じ場合。</exception>
    private async Task ValidatePasswordHistoryAsync(User user, string newPassword, CancellationToken ct)
    {
        var recentPasswords = await passwordHistoryRepository.FindRecentByUserIdAsync(
            user.Id, PasswordHistoryCount, ct);

        foreach (var history in recentPasswords)
        {
            var result = passwordHasher.VerifyHashedPassword(user, history.PasswordHash, newPassword);
            if (result != PasswordVerificationResult.Failed)
            {
                throw new BusinessException("過去に使用したパスワードは再利用できません");
            }
        }
    }
}
