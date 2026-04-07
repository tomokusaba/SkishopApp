using AuthService.DTOs.Responses;
using AuthService.Models;

namespace AuthService.Services.Interfaces;

/// <summary>
/// JWTトークンサービスのインターフェース。
/// アクセストークンとリフレッシュトークンの生成、検証、無効化を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>アクセストークンは短時間（通常1時間以下）で期限切れになるよう設定してください</item>
///   <item>署名キーは最低32文字以上の強力なキーを使用してください</item>
///   <item>トークンに機密情報（パスワードハッシュなど）を含めないでください</item>
///   <item>無効化されたトークンはブラックリストで管理されます</item>
/// </list>
/// </remarks>
public interface IJwtTokenService
{
    /// <summary>
    /// ユーザー用のアクセストークン（JWT）を生成します。
    /// </summary>
    /// <param name="user">トークンを発行するユーザーエンティティ。</param>
    /// <param name="sessionId">関連付けるセッションID。</param>
    /// <returns>署名されたJWTアクセストークン文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> または <paramref name="sessionId"/> が null の場合。</exception>
    /// <exception cref="InvalidOperationException">JWT SecretKey が設定されていない、または長さが不足している場合。</exception>
    /// <remarks>
    /// トークンに含まれるクレーム:
    /// <list type="bullet">
    ///   <item>sub: ユーザーID</item>
    ///   <item>email: メールアドレス</item>
    ///   <item>jti: 一意のトークン識別子</item>
    ///   <item>role: ユーザーロール</item>
    ///   <item>session_id: セッションID</item>
    /// </list>
    /// </remarks>
    string GenerateAccessToken(User user, string sessionId);

    /// <summary>
    /// 暗号学的に安全なリフレッシュトークンを生成します。
    /// </summary>
    /// <returns>Base64エンコードされたランダムなリフレッシュトークン。</returns>
    /// <remarks>
    /// リフレッシュトークンの特性:
    /// <list type="bullet">
    ///   <item>32バイト（256ビット）のランダムデータから生成されます</item>
    ///   <item>JWTとは異なり、サーバー側で状態管理されます</item>
    ///   <item>トークンローテーションにより、使用後は新しいトークンに置き換えられます</item>
    /// </list>
    /// </remarks>
    string GenerateRefreshToken();

    /// <summary>
    /// JWTアクセストークンを検証します。
    /// </summary>
    /// <param name="token">検証するJWTトークン文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検証結果（有効性、ユーザーID、ロール、有効期限）を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> が null の場合。</exception>
    /// <exception cref="InvalidOperationException">JWT SecretKey が設定されていない、または長さが不足している場合。</exception>
    /// <remarks>
    /// 検証項目:
    /// <list type="bullet">
    ///   <item>署名の正当性</item>
    ///   <item>発行者（Issuer）の妥当性</item>
    ///   <item>対象者（Audience）の妥当性</item>
    ///   <item>有効期限</item>
    ///   <item>5分間のクロックスキューを許容</item>
    /// </list>
    /// </remarks>
    Task<TokenValidationResponse> ValidateTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// 指定されたトークンを無効化し、ブラックリストに追加します。
    /// </summary>
    /// <param name="tokenId">無効化するトークンのJTI（JWT ID）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokenId"/> が null の場合。</exception>
    /// <remarks>
    /// 無効化されたトークン:
    /// <list type="bullet">
    ///   <item>Redisなどのキャッシュでブラックリスト管理されます</item>
    ///   <item>元のトークンの有効期限までブラックリストに保持されます</item>
    ///   <item>ログアウトやセキュリティイベント時に使用されます</item>
    /// </list>
    /// </remarks>
    Task RevokeTokenAsync(string tokenId, CancellationToken ct = default);
}
