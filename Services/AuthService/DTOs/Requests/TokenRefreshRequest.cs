using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// トークンリフレッシュリクエスト DTO。
/// 有効期限切れのアクセストークンを新しいトークンに更新する。
/// POST /auth/token/refresh エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>RefreshToken: 必須。ログイン時に発行されたリフレッシュトークン</description></item>
/// </list>
/// <para>トークンライフサイクル:</para>
/// <list type="bullet">
///   <item><description>アクセストークン有効期限: 1時間（デフォルト）</description></item>
///   <item><description>リフレッシュトークン有効期限: 7日間（デフォルト）</description></item>
///   <item><description>リフレッシュ時、新しいアクセストークンとリフレッシュトークンの両方が発行される</description></item>
///   <item><description>古いリフレッシュトークンは無効化される（ローテーション方式）</description></item>
/// </list>
/// <para>ユーザーあたりの有効なリフレッシュトークン数は最大10個に制限される。</para>
/// </remarks>
/// <param name="RefreshToken">ログイン時またはトークンリフレッシュ時に発行されたリフレッシュトークン。</param>
public record TokenRefreshRequest(
    [Required(ErrorMessage = "リフレッシュトークンは必須です")]
    string RefreshToken);
