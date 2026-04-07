using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;

namespace AuthService.Services;

/// <summary>
/// <see cref="IOAuthService"/> の実装クラス。
/// 外部OAuthプロバイダーとの連携機能を提供します。
/// </summary>
/// <remarks>
/// 現在の実装状況:
/// <list type="bullet">
///   <item>OAuth認証コールバック: 未実装（将来のアップデートで対応予定）</item>
///   <item>アカウントリンク: 未実装（将来のアップデートで対応予定）</item>
///   <item>アカウントリンク解除: 実装済み</item>
///   <item>リンク済みアカウント一覧取得: 実装済み</item>
/// </list>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>パスワード未設定時は最後のOAuthリンクを解除できません</item>
///   <item>リンク解除操作はセキュリティログに記録されます</item>
/// </list>
/// </remarks>
/// <param name="oAuthAccountRepository">OAuthアカウントリポジトリ。</param>
/// <param name="userRepository">ユーザーリポジトリ。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="logger">ロガー。</param>
public class OAuthService(
    IOAuthAccountRepository oAuthAccountRepository,
    IUserRepository userRepository,
    ISecurityService securityService,
    ILogger<OAuthService> logger) : IOAuthService
{
    /// <inheritdoc />
    /// <remarks>
    /// 注意: この機能は現在実装中です。呼び出すと <see cref="BusinessException"/> がスローされます。
    /// </remarks>
    public Task<LoginResponse> HandleOAuthCallbackAsync(
        string provider,
        string code,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(code);

        logger.LogInformation("OAuth コールバック受信: Provider={Provider}", provider);

        throw new BusinessException($"OAuth プロバイダー '{provider}' は現在サポートされていません");
    }

    /// <inheritdoc />
    /// <remarks>
    /// 注意: この機能は現在実装中です。呼び出すと <see cref="BusinessException"/> がスローされます。
    /// 認可コードをそのままProviderUserIdに保存することはセキュリティ上問題があるため、
    /// 適切なアクセストークン交換が実装されるまでブロックされています。
    /// </remarks>
    public async Task LinkAccountAsync(
        string userId,
        string provider,
        string code,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(code);

        // OAuth 認可コード → アクセストークン交換 → ユーザー識別子取得は未実装
        // code をそのまま ProviderUserId に保存するのはセキュリティ上問題があるためブロック
        throw new BusinessException("OAuth アカウントリンクは現在利用できません。今後のアップデートで対応予定です。");
    }

    /// <inheritdoc />
    public async Task UnlinkAccountAsync(
        string userId,
        string provider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(provider);

        var accounts = await oAuthAccountRepository.FindByUserIdAsync(userId, ct);
        if (accounts.All(a => a.Provider != provider))
            throw new NotFoundException($"プロバイダー '{provider}' のリンクが見つかりません");

        var user = await userRepository.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません: {userId}");

        if (string.IsNullOrEmpty(user.PasswordHash) && accounts.Count <= 1)
        {
            throw new BusinessException("パスワード未設定のため、最後の OAuth リンクは解除できません");
        }

        await oAuthAccountRepository.DeleteByUserIdAndProviderAsync(userId, provider, ct);

        await securityService.LogSecurityEventAsync(
            userId, "OAUTH_UNLINKED", null, null, $"OAuth プロバイダー '{provider}' のリンクが解除されました", ct);

        logger.LogInformation("OAuth アカウントリンク解除: UserId={UserId}, Provider={Provider}", userId, provider);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OAuthAccountDto>> GetLinkedAccountsAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var accounts = await oAuthAccountRepository.FindByUserIdAsync(userId, ct);

        return accounts.Select(a => new OAuthAccountDto(
            a.Provider,
            a.ProviderUserId,
            a.CreatedAt)).ToList();
    }
}
