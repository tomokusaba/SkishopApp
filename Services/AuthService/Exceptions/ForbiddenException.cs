namespace AuthService.Exceptions;

/// <summary>
/// 認証済みだがリソースへのアクセス権限がない場合にスローされる例外。
/// HTTP 403 (Forbidden) にマッピングされる。
/// </summary>
/// <remarks>
/// <para>使用例:</para>
/// <list type="bullet">
///   <item><description>一般ユーザーが管理者専用エンドポイントにアクセス</description></item>
///   <item><description>他ユーザーのリソースへのアクセス試行（IDOR 対策）</description></item>
///   <item><description>無効化されたアカウントからのアクセス</description></item>
/// </list>
/// </remarks>
/// <param name="message">アクセス拒否の理由。省略時は "アクセスが拒否されました" が使用される。</param>
public class ForbiddenException(string? message = null)
    : Exception(message ?? "アクセスが拒否されました");
