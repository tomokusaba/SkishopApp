using System.Security.Cryptography;
using System.Text.Json;
using AuthService.Enums;
using AuthService.DTOs.Events;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Infrastructure.Metrics;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Services;

/// <summary>
/// <see cref="IUserRegistrationService"/> の実装クラス。
/// ユーザー登録、メール認証、およびアカウント削除機能を提供します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>パスワードのセキュアなハッシュ化（ASP.NET Core Identity）</item>
///   <item>メールアドレスとユーザー名の重複チェック</item>
///   <item>メール認証による本人確認</item>
///   <item>論理削除と物理削除（GDPR対応）のサポート</item>
///   <item>すべての操作のセキュリティログ記録</item>
///   <item>イベント駆動アーキテクチャ（Outboxパターン）</item>
/// </list>
/// </remarks>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="roleRepository">ロールリポジトリ。</param>
/// <param name="passwordResetRepository">パスワードリセット/メール認証トークンリポジトリ。</param>
/// <param name="passwordHasher">パスワードハッシャー。</param>
/// <param name="outboxEventRepository">Outboxイベントリポジトリ。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class UserRegistrationService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordResetRepository passwordResetRepository,
    IPasswordHasher<User> passwordHasher,
    IOutboxEventRepository outboxEventRepository,
    ISecurityService securityService,
    TimeProvider timeProvider,
    ILogger<UserRegistrationService> logger) : IUserRegistrationService
{
    /// <inheritdoc />
    public async Task<UserResponse> RegisterAsync(UserCreateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingByEmail = await userRepository.FindByEmailAsync(request.Email, ct);
        if (existingByEmail is not null)
        {
            throw new BusinessException("このメールアドレスは既に登録されています");
        }

        var existingByUsername = await userRepository.FindByUsernameAsync(request.Username, ct);
        if (existingByUsername is not null)
        {
            throw new BusinessException("このユーザー名は既に使用されています");
        }

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Status = UserStatus.PendingVerification,
            Role = UserRoleType.User,
            IsActive = true,
            IsEmailVerified = false
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await userRepository.AddAsync(user, ct);

        var role = await roleRepository.FindByNameAsync("USER", ct);
        if (role is not null)
        {
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            };
            user.UserRoles.Add(userRole);
        }

        var verificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var passwordReset = new PasswordReset
        {
            UserId = user.Id,
            Token = verificationToken,
            TokenType = "EMAIL_VERIFICATION",
            ExpiresAt = timeProvider.GetUtcNow().AddHours(24)
        };
        await passwordResetRepository.AddAsync(passwordReset, ct);

        var registeredEvent = new UserRegisteredEvent(
            Guid.NewGuid().ToString(),
            user.Id,
            user.Role.ToString().ToUpperInvariant(),
            timeProvider.GetUtcNow());

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.user.registered",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(registeredEvent)
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);

        var verificationOutbox = new OutboxEvent
        {
            EventType = "auth.email_verification.requested",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(new
            {
                UserId = user.Id,
                OccurredAt = timeProvider.GetUtcNow()
            })
        };
        await outboxEventRepository.AddAsync(verificationOutbox, ct);
        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            user.Id, "USER_REGISTERED", null, null, "ユーザー登録完了", ct);

        AuthMetrics.RegistrationAttempts.Add(1);

        logger.LogInformation("ユーザー登録完了: UserId={UserId}, Email={MaskedEmail}",
            user.Id, MaskEmail(user.Email));

        return new UserResponse(
            user.Id,
            user.Email,
            user.Username ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.Status.ToString().ToUpperInvariant(),
            user.Role.ToString().ToUpperInvariant(),
            user.CreatedAt);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        user.IsActive = false;
        user.Status = UserStatus.Suspended;
        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            userId, "USER_SOFT_DELETED", null, null, "ユーザーが無効化されました", ct);

        logger.LogInformation("ユーザー論理削除: UserId={UserId}", userId);
    }

    /// <inheritdoc />
    public async Task HardDeleteAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        user.IsActive = false;
        user.Status = UserStatus.Suspended;
        user.Email = $"deleted_{user.Id}@deleted.local";
        user.Username = null;
        user.PasswordHash = null;
        user.FirstName = null;
        user.LastName = null;
        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            userId, "USER_HARD_DELETED", null, null, "ユーザーデータが消去されました", ct);

        logger.LogInformation("ユーザー物理削除: UserId={UserId}", userId);
    }

    /// <inheritdoc />
    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var resetToken = await passwordResetRepository.FindByTokenAsync(token, ct)
            ?? throw new BusinessException("無効な認証トークンです");

        if (resetToken.TokenType != "EMAIL_VERIFICATION")
        {
            throw new BusinessException("無効なトークンタイプです");
        }

        if (resetToken.IsUsed)
        {
            throw new BusinessException("このトークンは既に使用されています");
        }

        if (resetToken.ExpiresAt < timeProvider.GetUtcNow())
        {
            throw new BusinessException("トークンの有効期限が切れています");
        }

        var user = await userRepository.FindByIdAsync(resetToken.UserId, ct)
            ?? throw new NotFoundException("ユーザーが見つかりません");

        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;

        resetToken.IsUsed = true;
        resetToken.UsedAt = timeProvider.GetUtcNow();

        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            user.Id, "EMAIL_VERIFIED", null, null, "メールアドレスが認証されました", ct);

        logger.LogInformation("メール認証完了: UserId={UserId}", user.Id);
    }

    /// <inheritdoc />
    public async Task ResendVerificationEmailAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        if (user.IsEmailVerified)
        {
            throw new BusinessException("メールアドレスは既に認証済みです");
        }

        await passwordResetRepository.InvalidateExistingTokensAsync(userId, "EMAIL_VERIFICATION", ct);

        var verificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var passwordReset = new PasswordReset
        {
            UserId = userId,
            Token = verificationToken,
            TokenType = "EMAIL_VERIFICATION",
            ExpiresAt = timeProvider.GetUtcNow().AddHours(24)
        };
        await passwordResetRepository.AddAsync(passwordReset, ct);

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.email_verification.requested",
            Topic = "auth-events",
            Key = userId,
            Payload = JsonSerializer.Serialize(new
            {
                UserId = userId,
                OccurredAt = timeProvider.GetUtcNow()
            })
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        await outboxEventRepository.SaveChangesAsync(ct);

        logger.LogInformation("メール認証トークン再送: UserId={UserId}", userId);
    }

    /// <summary>
    /// メールアドレスをマスキングします（ログ出力用）。
    /// </summary>
    /// <param name="email">マスキングするメールアドレス。</param>
    /// <returns>マスキングされたメールアドレス（例: "t***@example.com"）。</returns>
    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***";
        return $"{email[0]}***@{email[(atIndex + 1)..]}";
    }
}
