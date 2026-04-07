namespace AuthService.Exceptions;

/// <summary>
/// 指定されたリソースが見つからない場合にスローされる例外。
/// HTTP 404 (Not Found) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>使用例:</para>
/// <list type="bullet">
///   <item><description>指定された ID のユーザーが存在しない</description></item>
///   <item><description>指定されたトークンに対応するセッションが存在しない</description></item>
///   <item><description>指定されたリフレッシュトークンが存在しないか期限切れ</description></item>
/// </list>
/// </remarks>
/// <param name="message">見つからなかったリソースの詳細を示すメッセージ。</param>
public class NotFoundException(string message) : Exception(message);
