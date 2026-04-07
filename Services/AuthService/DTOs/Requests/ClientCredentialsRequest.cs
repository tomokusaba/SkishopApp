using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// OAuth 2.0 Client Credentials グラントリクエスト DTO。
/// マシン間（M2M）認証に使用され、サービス間通信のアクセストークンを取得する。
/// POST /auth/oauth/token エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>ClientId: 必須、最大100文字</description></item>
///   <item><description>ClientSecret: 必須、最大255文字</description></item>
///   <item><description>Scope: 必須（例: "inventory:read sales:write"）</description></item>
///   <item><description>GrantType: 必須、"client_credentials" のみ許可</description></item>
/// </list>
/// </remarks>
/// <param name="ClientId">OAuth クライアント ID。事前に AuthService に登録されている必要がある。</param>
/// <param name="ClientSecret">OAuth クライアントシークレット。安全に管理されるべき秘密情報。</param>
/// <param name="Scope">リクエストするスコープ。スペース区切りで複数指定可能。</param>
/// <param name="GrantType">OAuth グラントタイプ。"client_credentials" を指定する。</param>
public record ClientCredentialsRequest(
    [Required(ErrorMessage = "クライアントIDは必須です")]
    [MaxLength(100)]
    string ClientId,

    [Required(ErrorMessage = "クライアントシークレットは必須です")]
    [MaxLength(255)]
    string ClientSecret,

    [Required(ErrorMessage = "スコープは必須です")]
    string Scope,

    [Required(ErrorMessage = "グラントタイプは必須です")]
    string GrantType);
