using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;

namespace AuthService.Services.Interfaces;

/// <summary>
/// 認証サービスのインターフェース。
/// ユーザーのログイン、ログアウト、トークンのリフレッシュ、MFA認証などの認証操作を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>ログイン試行回数の制限により、ブルートフォース攻撃を防止します</item>
///   <item>リフレッシュトークンはローテーション方式を採用し、リプレイ攻撃を検出します</item>
///   <item>MFA有効時は2段階認証が必須となります</item>
///   <item>すべての認証イベントはセキュリティログに記録されます</item>
/// </list>
/// </remarks>
public interface IAuthService
{
    /// <summary>
    /// ユーザーのログイン認証を実行します。
    /// </summary>
    /// <param name="request">メールアドレスとパスワードを含むログインリクエスト。</param>
    /// <param name="ipAddress">リクエスト元のIPアドレス。セキュリティログに記録されます。</param>
    /// <param name="userAgent">クライアントのUser-Agentヘッダー。セッション追跡に使用されます。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アクセストークン、リフレッシュトークン、およびユーザー情報を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    /// <exception cref="Exceptions.UnauthorizedException">認証情報が無効な場合。</exception>
    /// <exception cref="Exceptions.AccountLockedException">アカウントがロックされている場合。</exception>
    /// <exception cref="Exceptions.BusinessException">アカウントが有効でない場合（メール未認証など）。</exception>
    /// <exception cref="Exceptions.MfaRequiredException">MFAが有効で、追加認証が必要な場合。</exception>
    /// <exception cref="Exceptions.ConcurrencyException">データの楽観的ロック競合が発生した場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>パスワードはASP.NET Core Identityのハッシュ関数で検証されます</item>
    ///   <item>ログイン失敗時は失敗回数がカウントされ、上限超過でアカウントがロックされます</item>
    ///   <item>MFA有効時は <see cref="Exceptions.MfaRequiredException"/> がスローされ、別途 <see cref="CompleteMfaLoginAsync"/> を呼び出す必要があります</item>
    ///   <item>同時セッション数が上限に達した場合、最古のセッションが自動的に無効化されます</item>
    /// </list>
    /// </remarks>
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// リフレッシュトークンを使用して新しいアクセストークンを発行します。
    /// </summary>
    /// <param name="request">リフレッシュトークンを含むリクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>新しいアクセストークンとリフレッシュトークンを含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    /// <exception cref="Exceptions.UnauthorizedException">
    /// リフレッシュトークンが無効、期限切れ、または既に使用済みの場合。
    /// リプレイ攻撃が検出された場合、同一ファミリーの全トークンが無効化されます。
    /// </exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>リフレッシュトークンローテーション: 使用済みトークンは即座に無効化されます</item>
    ///   <item>リプレイ攻撃検出: 無効化済みトークンの再使用時、同一ファミリーの全トークンを無効化します</item>
    ///   <item>絶対有効期限: ファミリー全体の有効期限があり、永続的なセッションを防止します</item>
    /// </list>
    /// </remarks>
    Task<TokenRefreshResponse> RefreshTokenAsync(TokenRefreshRequest request, CancellationToken ct = default);

    /// <summary>
    /// ユーザーをログアウトし、関連するセッションとトークンを無効化します。
    /// </summary>
    /// <param name="sessionId">無効化するセッションのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sessionId"/> が null の場合。</exception>
    /// <remarks>
    /// ログアウト処理:
    /// <list type="bullet">
    ///   <item>指定されたセッションを非アクティブ化します</item>
    ///   <item>該当ユーザーのすべてのリフレッシュトークンを無効化します</item>
    ///   <item>セキュリティイベントとしてログアウトを記録します</item>
    /// </list>
    /// </remarks>
    Task LogoutAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 現在認証されているユーザーの情報を取得します。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ユーザーの詳細情報を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    Task<UserInfoResponse> GetCurrentUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// MFA認証を完了し、完全なログインを実行します。
    /// </summary>
    /// <param name="sessionToken">ログイン時に発行されたMFAセッショントークン。</param>
    /// <param name="code">TOTP認証コードまたはバックアップコード。</param>
    /// <param name="ipAddress">リクエスト元のIPアドレス。</param>
    /// <param name="userAgent">クライアントのUser-Agentヘッダー。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アクセストークン、リフレッシュトークン、およびユーザー情報を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sessionToken"/> または <paramref name="code"/> が null の場合。</exception>
    /// <exception cref="Exceptions.UnauthorizedException">
    /// MFAセッションが無効、期限切れ、またはMFAコードが無効な場合。
    /// </exception>
    /// <exception cref="Exceptions.NotFoundException">ユーザーが見つからない場合。</exception>
    /// <exception cref="Exceptions.ConcurrencyException">データの楽観的ロック競合が発生した場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>MFAセッションは短時間（5分）で期限切れになります</item>
    ///   <item>TOTPコードは前後1ステップ（30秒）の時間ずれを許容します</item>
    ///   <item>MFA認証成功後、ログイン失敗カウンターはリセットされます</item>
    /// </list>
    /// </remarks>
    Task<LoginResponse> CompleteMfaLoginAsync(
        string sessionToken, string code, string? ipAddress, string? userAgent,
        CancellationToken ct = default);
}
