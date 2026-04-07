using System.Text.Json;
using AuthService.Configurations;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

/// <summary>
/// <see cref="Interfaces.IAuthService"/> の実装クラス。
/// ユーザー認証、セッション管理、トークン発行を担当します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>ブルートフォース攻撃対策: ログイン失敗回数制限とアカウントロック</item>
///   <item>MFA対応: TOTP/バックアップコードによる2要素認証</item>
///   <item>トークンローテーション: リフレッシュトークンのローテーションとリプレイ攻撃検出</item>
///   <item>セッション管理: 同時セッション数制限と最古セッションの自動無効化</item>
///   <item>自動アンロック: 設定時間経過後のアカウント自動アンロック</item>
/// </list>
/// </remarks>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="userSessionRepository">ユーザーセッションリポジトリ。</param>
/// <param name="refreshTokenRepository">リフレッシュトークンリポジトリ。</param>
/// <param name="jwtTokenService">JWTトークンサービス。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="mfaService">MFAサービス。</param>
/// <param name="outboxEventRepository">Outboxイベントリポジトリ。</param>
/// <param name="passwordHasher">パスワードハッシャー。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="authSettings">認証設定。</param>
/// <param name="sessionSettings">セッション設定。</param>
/// <param name="logger">ロガー。</param>
public class AuthServiceImpl(
    IUserRepository userRepository,
    IUserSessionRepository userSessionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    ISecurityService securityService,
    IMfaService mfaService,
    IOutboxEventRepository outboxEventRepository,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider,
    IOptions<AuthSettings> authSettings,
    IOptions<SessionSettings> sessionSettings,
    ILogger<AuthServiceImpl> logger) : Interfaces.IAuthService
{
    private readonly AuthSettings _authSettings = authSettings.Value;
    private readonly SessionSettings _sessionSettings = sessionSettings.Value;

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userRepository.FindByEmailForLoginAsync(request.Email, ct)
            ?? throw new UnauthorizedException("メールアドレスまたはパスワードが正しくありません");

        if (user.IsAccountLocked)
        {
            var now = timeProvider.GetUtcNow();
            if (user.LockedAt.HasValue &&
                user.LockedAt.Value.AddMinutes(_authSettings.AutoUnlockMinutes) < now)
            {
                await securityService.UnlockAccountAsync(user.Id, ct);
            }
            else
            {
                await securityService.LogSecurityEventAsync(
                    user.Id, "LOGIN_ATTEMPT_LOCKED", ipAddress, userAgent, "アカウントがロックされています", ct);
                throw new AccountLockedException("アカウントがロックされています。しばらく経ってから再度お試しください。");
            }
        }

        if (user.Status != UserStatus.Active)
        {
            throw new BusinessException("アカウントが有効ではありません。メール認証を完了してください。");
        }

        var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            AuthMetrics.LoginAttempts.Add(1,
                new KeyValuePair<string, object?>("result", "failure"),
                new KeyValuePair<string, object?>("method", "password"));

            var locked = await securityService.IncrementFailedAttemptsAsync(user.Id, ct);
            await securityService.LogSecurityEventAsync(
                user.Id, "LOGIN_FAILED", ipAddress, userAgent, "パスワード不一致", ct);

            if (locked)
            {
                throw new AccountLockedException("ログイン試行回数が上限に達しました。アカウントがロックされました。");
            }

            throw new UnauthorizedException("メールアドレスまたはパスワードが正しくありません");
        }

        if (user.Mfa is { IsEnabled: true })
        {
            var mfaSessionToken = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

            var mfaSession = new UserSession
            {
                UserId = user.Id,
                SessionToken = mfaSessionToken,
                IpAddress = ipAddress,
                UserAgent = SanitizeUserAgent(userAgent),
                IsActive = true,
                ExpiresAt = timeProvider.GetUtcNow().AddMinutes(5),
                LastActivity = timeProvider.GetUtcNow()
            };
            await userSessionRepository.AddAsync(mfaSession, ct);
            await userSessionRepository.SaveChangesAsync(ct);

            throw new MfaRequiredException(mfaSessionToken);
        }

        await securityService.ResetFailedAttemptsAsync(user.Id, ct);

        user.LastLogin = timeProvider.GetUtcNow();

        var activeSessions = await userSessionRepository.FindActiveByUserIdAsync(user.Id, ct);
        if (activeSessions.Count >= _sessionSettings.MaxConcurrentSessions)
        {
            var oldestSession = activeSessions.OrderBy(s => s.LastActivity).First();
            await userSessionRepository.DeactivateSessionAsync(oldestSession.Id, ct);
            logger.LogInformation("セッション上限到達、最古セッション無効化: UserId={UserId}", user.Id);
        }

        var sessionToken = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var session = new UserSession
        {
            UserId = user.Id,
            SessionToken = sessionToken,
            IpAddress = ipAddress,
            UserAgent = SanitizeUserAgent(userAgent),
            IsActive = true,
            ExpiresAt = timeProvider.GetUtcNow().AddSeconds(_sessionSettings.Timeout),
            LastActivity = timeProvider.GetUtcNow()
        };
        await userSessionRepository.AddAsync(session, ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user, session.Id);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        var familyId = Guid.NewGuid().ToString();
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            Jti = Guid.NewGuid().ToString(),
            FamilyId = familyId,
            AbsoluteExpiry = timeProvider.GetUtcNow().AddDays(30),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(7)
        };
        await refreshTokenRepository.AddAsync(refreshTokenEntity, ct);

        var authEvent = new UserAuthenticatedEvent(
            Guid.NewGuid().ToString(),
            user.Id,
            session.Id,
            ipAddress,
            SanitizeUserAgent(userAgent),
            "PASSWORD",
            false,
            timeProvider.GetUtcNow());

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.login.success",
            Topic = "auth-events",
            Key = user.Id,
            Payload = JsonSerializer.Serialize(authEvent)
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);

        try
        {
            await userRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ex.Entries.FirstOrDefault()?.Entity.GetType().Name);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
        }

        await securityService.LogSecurityEventAsync(
            user.Id, "LOGIN_SUCCESS", ipAddress, userAgent, "パスワード認証成功", ct);

        AuthMetrics.LoginAttempts.Add(1,
            new KeyValuePair<string, object?>("result", "success"),
            new KeyValuePair<string, object?>("method", "password"));

        logger.LogInformation("ユーザーログイン成功: UserId={UserId}", user.Id);

        return new LoginResponse(
            accessToken,
            refreshToken,
            "Bearer",
            3600,
            new UserDto(user.Id, user.FirstName ?? string.Empty, user.LastName ?? string.Empty, user.Role.ToString().ToUpperInvariant()));
    }

    /// <inheritdoc />
    public async Task<TokenRefreshResponse> RefreshTokenAsync(
        TokenRefreshRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingToken = await refreshTokenRepository.FindByTokenAsync(request.RefreshToken, ct)
            ?? throw new UnauthorizedException("無効なリフレッシュトークンです");

        if (existingToken.IsRevoked)
        {
            logger.LogWarning("リプレイ攻撃検出: FamilyId={FamilyId}, UserId={UserId}",
                existingToken.FamilyId, existingToken.UserId);

            await refreshTokenRepository.RevokeAllByFamilyIdAsync(
                existingToken.FamilyId, timeProvider.GetUtcNow(), ct);
            await refreshTokenRepository.SaveChangesAsync(ct);

            await securityService.LogSecurityEventAsync(
                existingToken.UserId, "TOKEN_REPLAY_ATTACK", null, null,
                $"FamilyId={existingToken.FamilyId}", ct);

            throw new UnauthorizedException("セキュリティ上の理由によりトークンが無効化されました");
        }

        var now = timeProvider.GetUtcNow();

        if (existingToken.ExpiresAt < now)
        {
            throw new UnauthorizedException("リフレッシュトークンの有効期限が切れています");
        }

        if (existingToken.AbsoluteExpiry < now)
        {
            await refreshTokenRepository.RevokeAllByFamilyIdAsync(
                existingToken.FamilyId, now, ct);
            await refreshTokenRepository.SaveChangesAsync(ct);
            throw new UnauthorizedException("リフレッシュトークンの絶対有効期限を超過しています");
        }

        existingToken.IsRevoked = true;
        existingToken.RevokedAt = now;

        var user = await userRepository.FindByIdAsync(existingToken.UserId, ct)
            ?? throw new UnauthorizedException("ユーザーが見つかりません");

        var newAccessToken = jwtTokenService.GenerateAccessToken(user, Guid.NewGuid().ToString());
        var newRefreshTokenValue = jwtTokenService.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            Token = newRefreshTokenValue,
            Jti = Guid.NewGuid().ToString(),
            FamilyId = existingToken.FamilyId,
            PreviousTokenId = existingToken.Id,
            AbsoluteExpiry = existingToken.AbsoluteExpiry,
            ExpiresAt = now.AddDays(7)
        };

        existingToken.ReplacedByToken = newRefreshToken.Id;

        await refreshTokenRepository.AddAsync(newRefreshToken, ct);
        await refreshTokenRepository.SaveChangesAsync(ct);

        AuthMetrics.TokenRefreshes.Add(1);

        logger.LogInformation("トークンリフレッシュ成功: UserId={UserId}", existingToken.UserId);

        return new TokenRefreshResponse(newAccessToken, newRefreshTokenValue, 3600);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var session = await userSessionRepository.FindBySessionIdAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("ログアウト対象セッションが見つかりません: SessionId={SessionId}", sessionId);
            return;
        }

        session.IsActive = false;
        await userSessionRepository.SaveChangesAsync(ct);

        await refreshTokenRepository.RevokeAllByUserIdAsync(
            session.UserId, timeProvider.GetUtcNow(), ct);
        await refreshTokenRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            session.UserId, "LOGOUT", session.IpAddress, session.UserAgent, "ユーザーログアウト", ct);

        logger.LogInformation("ユーザーログアウト: UserId={UserId}", session.UserId);
    }

    /// <inheritdoc />
    public async Task<UserInfoResponse> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        return new UserInfoResponse(
            user.Id,
            user.Email,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            user.Role.ToString().ToUpperInvariant(),
            user.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> CompleteMfaLoginAsync(
        string sessionToken, string code, string? ipAddress, string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        ArgumentNullException.ThrowIfNull(code);

        var mfaSession = await userSessionRepository.FindBySessionTokenAsync(sessionToken, ct)
            ?? throw new UnauthorizedException("MFA セッションが無効です");

        if (!mfaSession.IsActive || mfaSession.ExpiresAt < timeProvider.GetUtcNow())
        {
            throw new UnauthorizedException("MFA セッションが期限切れです");
        }

        var userId = mfaSession.UserId;

        var isValid = await mfaService.VerifyMfaAsync(userId, code, ct);
        if (!isValid)
            throw new UnauthorizedException("MFA 認証コードが無効です");

        mfaSession.IsActive = false;
        await userSessionRepository.SaveChangesAsync(ct);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        await securityService.ResetFailedAttemptsAsync(userId, ct);

        var newSessionToken = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var session = new UserSession
        {
            UserId = userId,
            SessionToken = newSessionToken,
            IpAddress = ipAddress ?? mfaSession.IpAddress,
            UserAgent = SanitizeUserAgent(userAgent ?? mfaSession.UserAgent),
            IsActive = true,
            ExpiresAt = timeProvider.GetUtcNow().AddSeconds(_sessionSettings.Timeout),
            LastActivity = timeProvider.GetUtcNow()
        };
        await userSessionRepository.AddAsync(session, ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user, session.Id);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        var familyId = Guid.NewGuid().ToString();
        var refreshTokenEntity = new RefreshToken
        {
            UserId = userId,
            Token = refreshToken,
            Jti = Guid.NewGuid().ToString(),
            FamilyId = familyId,
            AbsoluteExpiry = timeProvider.GetUtcNow().AddDays(30),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(7)
        };
        await refreshTokenRepository.AddAsync(refreshTokenEntity, ct);

        var authEvent = new UserAuthenticatedEvent(
            Guid.NewGuid().ToString(),
            userId,
            session.Id,
            ipAddress ?? mfaSession.IpAddress,
            SanitizeUserAgent(userAgent ?? mfaSession.UserAgent),
            "MFA",
            true,
            timeProvider.GetUtcNow());

        var outboxEvent = new OutboxEvent
        {
            EventType = "auth.login.success",
            Topic = "auth-events",
            Key = userId,
            Payload = JsonSerializer.Serialize(authEvent)
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);

        try
        {
            await userRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ex.Entries.FirstOrDefault()?.Entity.GetType().Name);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
        }

        await securityService.LogSecurityEventAsync(
            userId, "LOGIN_SUCCESS_MFA", ipAddress ?? mfaSession.IpAddress,
            userAgent ?? mfaSession.UserAgent, "MFA 認証成功", ct);

        AuthMetrics.LoginAttempts.Add(1,
            new KeyValuePair<string, object?>("result", "success"),
            new KeyValuePair<string, object?>("method", "mfa"));

        logger.LogInformation("MFA ログイン成功: UserId={UserId}", userId);

        return new LoginResponse(
            accessToken,
            refreshToken,
            "Bearer",
            3600,
            new UserDto(user.Id, user.FirstName ?? string.Empty, user.LastName ?? string.Empty, user.Role.ToString().ToUpperInvariant()));
    }

    /// <summary>
    /// User-Agent文字列をサニタイズします。
    /// </summary>
    /// <param name="userAgent">元のUser-Agent文字列。</param>
    /// <returns>制御文字を除去し、最大500文字に制限されたUser-Agent文字列。</returns>
    private static string SanitizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Unknown";
        var sanitized = new string(userAgent.Where(c => !char.IsControl(c)).ToArray());
        return sanitized.Length > 500 ? sanitized[..500] : sanitized;
    }
}
