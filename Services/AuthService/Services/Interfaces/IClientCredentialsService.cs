using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;

namespace AuthService.Services.Interfaces;

/// <summary>
/// OAuth 2.0 Client Credentials Grant サービスのインターフェース。
/// サービス間認証（M2M: Machine-to-Machine）のためのトークン発行を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>クライアントシークレットは安全に保管し、ログに出力しないでください</item>
///   <item>スコープは最小権限の原則に基づいて設定してください</item>
///   <item>クライアント認証の失敗はセキュリティログに記録されます</item>
///   <item>発行されるトークンにはユーザーコンテキストは含まれません</item>
/// </list>
/// </remarks>
public interface IClientCredentialsService
{
    /// <summary>
    /// クライアント認証情報を検証し、アクセストークンを発行します。
    /// </summary>
    /// <param name="request">クライアントID、クライアントシークレット、およびスコープを含むリクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アクセストークンと有効期限情報を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// グラントタイプが "client_credentials" でない場合、
    /// クライアントが無効化されている場合、
    /// または要求されたスコープが許可されていない場合。
    /// </exception>
    /// <exception cref="Exceptions.UnauthorizedException">クライアント認証情報が無効な場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>クライアントシークレットはASP.NET Core Identityのハッシュ関数で検証されます</item>
    ///   <item>無効化されたクライアントはトークンを取得できません</item>
    ///   <item>要求されたスコープはクライアントに許可されたスコープと照合されます</item>
    ///   <item>トークンにはclient_idとスコープがクレームとして含まれます</item>
    /// </list>
    /// </remarks>
    Task<ClientCredentialsResponse> IssueTokenAsync(ClientCredentialsRequest request, CancellationToken ct = default);
}
