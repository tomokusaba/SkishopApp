namespace AuthService.Exceptions;

/// <summary>
/// ビジネスルール違反が検出された場合にスローされる例外。
/// HTTP 422 (Unprocessable Entity) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>使用例:</para>
/// <list type="bullet">
///   <item><description>パスワードポリシー違反（強度不足、過去のパスワードと同一等）</description></item>
///   <item><description>メールアドレスの重複登録</description></item>
///   <item><description>無効なトークンの使用</description></item>
///   <item><description>リフレッシュトークンの上限超過</description></item>
/// </list>
/// </remarks>
/// <param name="message">ビジネスルール違反の詳細を示すメッセージ。</param>
public class BusinessException(string message) : Exception(message);
