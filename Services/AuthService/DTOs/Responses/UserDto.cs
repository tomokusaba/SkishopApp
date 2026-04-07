namespace AuthService.DTOs.Responses;

/// <summary>
/// ユーザー基本情報 DTO。
/// ログインレスポンスやトークン情報に含まれる最小限のユーザー情報。
/// 他のマイクロサービスへの伝搬にも使用される軽量な DTO。
/// </summary>
/// <param name="Id">ユーザーの一意識別子（UUID 形式）。</param>
/// <param name="FirstName">ユーザーの名。</param>
/// <param name="LastName">ユーザーの姓。</param>
/// <param name="Role">ユーザーのロール。"USER" または "ADMIN"。</param>
public record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string Role);
