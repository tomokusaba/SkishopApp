namespace AuthService.Exceptions;

/// <summary>
/// 楽観的ロックの競合が発生した場合にスローされる例外。
/// HTTP 409 (Conflict) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>発生パターン:</para>
/// <list type="bullet">
///   <item><description>同一ユーザーの同時更新（複数デバイスからの同時パスワード変更等）</description></item>
///   <item><description>EF Core の DbUpdateConcurrencyException をラップ</description></item>
/// </list>
/// <para>対処方法:</para>
/// <list type="bullet">
///   <item><description>クライアントは最新データを再取得して操作を再試行する</description></item>
///   <item><description>ユーザーに競合が発生したことを通知する</description></item>
/// </list>
/// </remarks>
/// <param name="message">競合の詳細を示すメッセージ。</param>
public class ConcurrencyException(string message) : Exception(message);
