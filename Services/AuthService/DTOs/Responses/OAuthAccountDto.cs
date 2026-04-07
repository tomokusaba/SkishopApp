namespace AuthService.DTOs.Responses;

/// <summary>
/// OAuth アカウント連携情報 DTO。
/// ユーザーに紐づいた外部 OAuth プロバイダーアカウントの情報。
/// ユーザー情報取得時に連携済みの OAuth アカウントリストとして返される。
/// </summary>
/// <param name="Provider">OAuth プロバイダー名。"google", "microsoft", "github" 等。</param>
/// <param name="ProviderUserId">プロバイダー側のユーザー ID。プロバイダーごとに一意。</param>
/// <param name="CreatedAt">アカウント連携日時（UTC）。</param>
public record OAuthAccountDto(
    string Provider,
    string ProviderUserId,
    DateTimeOffset CreatedAt);
