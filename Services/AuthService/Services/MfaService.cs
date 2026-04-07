using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Infrastructure.Metrics;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;

namespace AuthService.Services;

/// <summary>
/// <see cref="IMfaService"/> の実装クラス。
/// TOTP（時間ベースワンタイムパスワード）による多要素認証の設定、検証、管理を担当します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>TOTPシークレットの暗号化保存</item>
///   <item>バックアップコードの生成（8個）</item>
///   <item>MFA有効化/無効化のセキュリティログ記録</item>
///   <item>メトリクスによるMFA検証の監視</item>
/// </list>
/// </remarks>
/// <param name="mfaRepository">MFAリポジトリ。</param>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="totpService">TOTPサービス。</param>
/// <param name="encryptionService">暗号化サービス。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class MfaService(
    IMfaRepository mfaRepository,
    IUserRepository userRepository,
    ITotpService totpService,
    IEncryptionService encryptionService,
    ISecurityService securityService,
    TimeProvider timeProvider,
    ILogger<MfaService> logger) : IMfaService
{
    /// <inheritdoc />
    public async Task<MfaSetupResponse> SetupMfaAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        var existingMfa = await mfaRepository.FindByUserIdAsync(userId, ct);
        if (existingMfa is { IsEnabled: true })
        {
            throw new BusinessException("MFA は既に有効化されています");
        }

        var secret = await totpService.GenerateSecretAsync(ct);
        var qrCodeUri = totpService.GenerateQrCodeUri(user.Email, secret);
        var backupCodes = totpService.GenerateBackupCodes(8);

        if (existingMfa is not null)
        {
            existingMfa.SecretKey = encryptionService.Encrypt(secret);
            existingMfa.IsEnabled = false;
            existingMfa.BackupCodes = System.Text.Json.JsonSerializer.Serialize(backupCodes);
        }
        else
        {
            var mfa = new UserMfa
            {
                UserId = userId,
                SecretKey = encryptionService.Encrypt(secret),
                IsEnabled = false,
                BackupCodes = System.Text.Json.JsonSerializer.Serialize(backupCodes)
            };
            await mfaRepository.AddAsync(mfa, ct);
        }

        await mfaRepository.SaveChangesAsync(ct);

        logger.LogInformation("MFA セットアップ開始: UserId={UserId}", userId);

        return new MfaSetupResponse(secret, qrCodeUri, backupCodes);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyMfaAsync(string userId, string code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(code);

        var mfa = await mfaRepository.FindByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("MFA が設定されていません");

        var decryptedSecret = encryptionService.Decrypt(mfa.SecretKey);
        var isValid = totpService.VerifyCode(decryptedSecret, code);

        if (!isValid)
        {
            AuthMetrics.MfaVerifications.Add(1,
                new KeyValuePair<string, object?>("result", "failure"));

            logger.LogWarning("MFA コード検証失敗: UserId={UserId}", userId);
            return false;
        }

        if (!mfa.IsEnabled)
        {
            mfa.IsEnabled = true;
            mfa.VerifiedAt = timeProvider.GetUtcNow();
            await mfaRepository.SaveChangesAsync(ct);

            await securityService.LogSecurityEventAsync(
                userId, "MFA_ENABLED", null, null, "MFA が有効化されました", ct);
        }

        AuthMetrics.MfaVerifications.Add(1,
            new KeyValuePair<string, object?>("result", "success"));

        logger.LogInformation("MFA コード検証成功: UserId={UserId}", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task DisableMfaAsync(string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var mfa = await mfaRepository.FindByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("MFA が設定されていません");

        mfa.IsEnabled = false;
        await mfaRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(
            userId, "MFA_DISABLED", null, null, "MFA が無効化されました", ct);

        logger.LogInformation("MFA 無効化: UserId={UserId}", userId);
    }
}
