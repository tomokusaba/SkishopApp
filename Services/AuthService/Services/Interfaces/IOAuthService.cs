using AuthService.DTOs.Responses;

namespace AuthService.Services.Interfaces;

/// <summary>
/// OAuth認証サービスのインターフェース。
/// 外部OAuthプロバイダー（Google、GitHub、Microsoftなど）との連携を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>認可コードは一度だけ使用可能で、短時間で期限切れになります</item>
///   <item>アクセストークンはサーバー側でのみ処理し、クライアントに漏洩させないでください</item>
///   <item>OAuthアカウントのリンク/リンク解除はセキュリティログに記録されます</item>
///   <item>パスワード未設定時は最後のOAuthリンクを解除できません</item>
/// </list>
/// </remarks>
public interface IOAuthService
{
    /// <summary>
    /// OAuthプロバイダーからのコールバックを処理し、ユーザーをログインさせます。
    /// </summary>
    /// <param name="provider">OAuthプロバイダー名（例: "google", "github", "microsoft"）。</param>
    /// <param name="code">プロバイダーから受け取った認可コード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アクセストークンとユーザー情報を含むログインレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> または <paramref name="code"/> が null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// プロバイダーがサポートされていない場合、
    /// またはOAuth認証に失敗した場合。
    /// </exception>
    /// <remarks>
    /// 現在の実装状況:
    /// <list type="bullet">
    ///   <item>この機能は現在実装中で、呼び出すと BusinessException がスローされます</item>
    ///   <item>将来的にGoogle、GitHub、Microsoftなどのプロバイダーをサポート予定です</item>
    /// </list>
    /// </remarks>
    Task<LoginResponse> HandleOAuthCallbackAsync(string provider, string code, CancellationToken ct = default);

    /// <summary>
    /// 既存のユーザーアカウントにOAuthプロバイダーをリンクします。
    /// </summary>
    /// <param name="userId">リンク先のユーザーID。</param>
    /// <param name="provider">OAuthプロバイダー名。</param>
    /// <param name="code">プロバイダーから受け取った認可コード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException">いずれかのパラメータが null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// OAuth連携が現在利用できない場合（実装中）、
    /// または同じプロバイダーアカウントが既にリンクされている場合。
    /// </exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>この機能は現在実装中です</item>
    ///   <item>リンク時にはプロバイダーでの認証が必要です</item>
    ///   <item>リンク操作はセキュリティログに記録されます</item>
    /// </list>
    /// </remarks>
    Task LinkAccountAsync(string userId, string provider, string code, CancellationToken ct = default);

    /// <summary>
    /// ユーザーアカウントからOAuthプロバイダーのリンクを解除します。
    /// </summary>
    /// <param name="userId">リンクを解除するユーザーID。</param>
    /// <param name="provider">リンクを解除するOAuthプロバイダー名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> または <paramref name="provider"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">
    /// 指定されたプロバイダーのリンクが見つからない場合、
    /// またはユーザーが存在しない場合。
    /// </exception>
    /// <exception cref="Exceptions.BusinessException">
    /// パスワードが設定されておらず、最後のOAuthリンクを解除しようとした場合。
    /// </exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>パスワード未設定の場合、ログイン手段を維持するため最後のOAuthリンクは解除できません</item>
    ///   <item>リンク解除操作はセキュリティログに記録されます</item>
    /// </list>
    /// </remarks>
    Task UnlinkAccountAsync(string userId, string provider, CancellationToken ct = default);

    /// <summary>
    /// ユーザーにリンクされているOAuthアカウントの一覧を取得します。
    /// </summary>
    /// <param name="userId">取得対象のユーザーID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>リンクされているOAuthアカウント情報のリスト。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    Task<IReadOnlyList<OAuthAccountDto>> GetLinkedAccountsAsync(string userId, CancellationToken ct = default);
}
