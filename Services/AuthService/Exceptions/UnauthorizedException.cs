namespace AuthService.Exceptions;

/// <summary>
/// 認証されていないリクエストに対してスローされる例外。
/// HTTP 401 (Unauthorized) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>使用例:</para>
/// <list type="bullet">
///   <item><description>認証ヘッダーが存在しない</description></item>
///   <item><description>JWT トークンが無効または期限切れ</description></item>
///   <item><description>パスワード認証の失敗</description></item>
///   <item><description>リフレッシュトークンが無効</description></item>
/// </list>
/// </remarks>
/// <param name="message">認証失敗の理由。省略時は "認証が必要です" が使用される。</param>
public class UnauthorizedException(string? message = null)
    : Exception(message ?? "認証が必要です");
