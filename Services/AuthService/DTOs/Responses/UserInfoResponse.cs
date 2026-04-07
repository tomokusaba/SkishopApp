namespace AuthService.DTOs.Responses;

/// <summary>
/// ユーザー情報レスポンス DTO。
/// 認証済みユーザーが自分のアカウント情報を取得する際に返される。
/// GET /auth/me エンドポイントのレスポンスとして使用される。
/// </summary>
/// <param name="Id">ユーザーの一意識別子（UUID 形式）。</param>
/// <param name="Email">ユーザーのメールアドレス。</param>
/// <param name="FirstName">ユーザーの名。</param>
/// <param name="LastName">ユーザーの姓。</param>
/// <param name="Role">ユーザーのロール。"USER" または "ADMIN"。</param>
/// <param name="CreatedAt">アカウント作成日時（UTC）。</param>
public record UserInfoResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTimeOffset CreatedAt);
