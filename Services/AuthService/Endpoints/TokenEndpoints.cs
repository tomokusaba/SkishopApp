using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// OAuth 2.0 Client Credentials Grant に関する API エンドポイントを提供するクラス。
/// マシン間通信（M2M）やサービス間認証用のアクセストークン発行機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「auth」レート制限ポリシーを適用。</para>
/// <para><b>OAuth 2.0 Client Credentials Grant (RFC 6749 Section 4.4)</b>: 
/// ユーザーコンテキストを持たないサービス間通信に使用。
/// クライアント ID とクライアントシークレットで認証し、アクセストークンを発行。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// クライアントシークレットは安全に管理し、定期的にローテーションする必要がある。
/// 本番環境では TLS（HTTPS）必須。</para>
/// <para><b>Use Cases</b>: 
/// - マイクロサービス間の API 呼び出し
/// - バッチ処理やスケジュールタスクからの API アクセス
/// - 外部システムとのインテグレーション</para>
/// </remarks>
public static class TokenEndpoints
{
    /// <summary>
    /// トークンエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        // トークングループ: /api/v1/auth/token 配下に全エンドポイントを配置
        // auth レート制限ポリシーを適用してブルートフォース攻撃を防止
        var group = app.MapGroup("/api/v1/auth/token")
            .WithTags("Token")
            .RequireRateLimiting("auth");

        // POST /api/v1/auth/token - Client Credentials でアクセストークンを発行
        // 匿名アクセス許可: クライアント認証はリクエストボディで行う
        // サービス間通信やバッチ処理用のトークン発行に使用
        group.MapPost("/", IssueClientCredentialsTokenAsync)
            .AllowAnonymous()
            .WithName("IssueClientCredentialsToken")
            .Produces<ClientCredentialsResponse>(200)
            .ProducesValidationProblem()
            .Produces(401);
    }

    /// <summary>
    /// Client Credentials Grant でアクセストークンを発行する。
    /// </summary>
    /// <remarks>
    /// <para><b>Authentication</b>: クライアント ID とクライアントシークレットで認証。
    /// Basic 認証ヘッダーまたはリクエストボディでの資格情報送信をサポート。</para>
    /// <para><b>Token Scope</b>: 発行されるトークンはクライアントに許可されたスコープのみ含む。
    /// リクエストで指定されたスコープがクライアントの許可範囲を超える場合はエラー。</para>
    /// <para><b>Token Lifetime</b>: アクセストークンの有効期限はクライアント設定に依存（デフォルト 1 時間）。
    /// リフレッシュトークンは発行されない（Client Credentials Grant の標準仕様）。</para>
    /// <para><b>Audit Log</b>: トークン発行はセキュリティログに記録。クライアント ID と発行スコープを記録。</para>
    /// <para><b>Security Considerations</b>:
    /// - クライアントシークレットは安全に保管（環境変数、シークレットマネージャー等）
    /// - 定期的なシークレットローテーションを推奨
    /// - 使用されていないクライアントは無効化
    /// - 最小権限の原則に基づきスコープを制限</para>
    /// </remarks>
    private static async Task<IResult> IssueClientCredentialsTokenAsync(
        [FromBody] ClientCredentialsRequest request,
        IValidator<ClientCredentialsRequest> validator,
        IClientCredentialsService clientCredentialsService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（クライアント ID、シークレット形式）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // クライアント認証とトークン発行
        return Results.Ok(await clientCredentialsService.IssueTokenAsync(request, ct));
    }
}
