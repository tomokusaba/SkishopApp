namespace AuthService.DTOs.Responses;

/// <summary>
/// トークン検証レスポンス DTO。
/// JWT アクセストークンの有効性を検証した結果を返す。
/// 内部サービス間のトークン検証や、API Gateway でのトークン検証に使用される。
/// </summary>
/// <param name="IsValid">トークンが有効かどうか。署名検証・有効期限・失効状態を確認。</param>
/// <param name="UserId">トークンに含まれるユーザー ID。無効なトークンの場合は null。</param>
/// <param name="Role">トークンに含まれるユーザーロール。無効なトークンの場合は null。</param>
/// <param name="ExpiresAt">トークンの有効期限（UTC）。無効なトークンの場合は null。</param>
public record TokenValidationResponse(
    bool IsValid,
    string? UserId,
    string? Role,
    DateTimeOffset? ExpiresAt);
