namespace AuthService.Exceptions;

/// <summary>
/// 多要素認証（MFA）が必要な場合にスローされる例外。
/// HTTP 401 (Unauthorized) にマッピングされ、レスポンスボディに SessionToken を含む。
/// </summary>
/// <remarks>
/// <para>発生条件:</para>
/// <list type="bullet">
///   <item><description>MFA が有効なユーザーがパスワード認証に成功した後</description></item>
///   <item><description>パスワード認証は完了したが、TOTP コードの検証が未完了</description></item>
/// </list>
/// <para>クライアント側の処理:</para>
/// <list type="number">
///   <item><description>SessionToken をレスポンスから取得</description></item>
///   <item><description>ユーザーに TOTP コードの入力を促す</description></item>
///   <item><description>SessionToken と TOTP コードを MfaVerificationRequest で送信</description></item>
/// </list>
/// </remarks>
/// <param name="sessionToken">MFA 認証用の一時セッショントークン。有効期限5分。</param>
public class MfaRequiredException(string sessionToken)
    : Exception("MFA 認証が必要です")
{
    /// <summary>MFA 認証を完了するために必要な一時セッショントークン。</summary>
    public string SessionToken { get; } = sessionToken;
}
