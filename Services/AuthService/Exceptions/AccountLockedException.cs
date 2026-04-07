namespace AuthService.Exceptions;

/// <summary>
/// アカウントがロックされている場合にスローされる例外。
/// HTTP 423 (Locked) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>ロックの原因:</para>
/// <list type="bullet">
///   <item><description>連続したログイン失敗回数が上限を超過した</description></item>
///   <item><description>管理者によるアカウントの一時停止</description></item>
///   <item><description>セキュリティポリシー違反の検出</description></item>
/// </list>
/// <para>ロックの解除方法:</para>
/// <list type="bullet">
///   <item><description>設定された時間（AutoUnlockMinutes）経過後に自動解除</description></item>
///   <item><description>管理者による手動解除</description></item>
///   <item><description>パスワードリセットフローの完了</description></item>
/// </list>
/// </remarks>
/// <param name="message">ロックの理由を示すメッセージ。</param>
public class AccountLockedException(string message) : Exception(message);
