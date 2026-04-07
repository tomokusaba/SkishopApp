namespace MailSendService.Services.Interfaces;

/// <summary>
/// メール送信に必要なユーザー情報を表す DTO。
/// </summary>
/// <param name="Id">ユーザーの一意識別子。</param>
/// <param name="Email">ユーザーのメールアドレス。</param>
/// <param name="FirstName">ユーザーの名。</param>
/// <param name="LastName">ユーザーの姓。</param>
public record UserInfo(string Id, string Email, string FirstName, string LastName);

/// <summary>
/// 顧客 ID からユーザー情報を解決するサービスインターフェース。
/// </summary>
public interface IUserInfoResolver
{
    /// <summary>
    /// 顧客 ID に対応するユーザー情報を取得する。
    /// </summary>
    /// <param name="customerId">解決対象の顧客 ID。</param>
    /// <param name="correlationId">分散トレーシング用の相関 ID（省略時は Activity.Current?.Id を使用）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ユーザー情報。該当ユーザーが存在しない場合は <c>null</c>。</returns>
    Task<UserInfo?> ResolveAsync(string customerId, string? correlationId = null, CancellationToken ct = default);
}
