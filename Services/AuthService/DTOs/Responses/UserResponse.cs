namespace AuthService.DTOs.Responses;

/// <summary>
/// ユーザー詳細レスポンス DTO。
/// ユーザー登録成功時や管理者によるユーザー情報取得時に返される詳細情報。
/// POST /auth/register および GET /auth/users/{id} エンドポイントのレスポンスとして使用される。
/// </summary>
/// <param name="Id">ユーザーの一意識別子（UUID 形式）。</param>
/// <param name="Email">ユーザーのメールアドレス。</param>
/// <param name="Username">ユーザーの一意ユーザー名。</param>
/// <param name="FirstName">ユーザーの名（任意）。</param>
/// <param name="LastName">ユーザーの姓（任意）。</param>
/// <param name="Status">アカウント状態。"PENDING_VERIFICATION"、"ACTIVE"、"SUSPENDED"、"LOCKED" 等。</param>
/// <param name="Role">ユーザーのロール。"USER" または "ADMIN"。</param>
/// <param name="CreatedAt">アカウント作成日時（UTC）。</param>
public record UserResponse(
    string Id,
    string Email,
    string Username,
    string? FirstName,
    string? LastName,
    string Status,
    string Role,
    DateTimeOffset CreatedAt);
