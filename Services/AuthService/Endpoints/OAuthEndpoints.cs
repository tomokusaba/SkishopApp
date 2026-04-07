using System.Security.Claims;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// OAuth 2.0 ソーシャルログイン連携に関する API エンドポイントを提供するクラス。
/// 外部 ID プロバイダー（Google, Microsoft, GitHub 等）とのアカウント連携機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「auth」レート制限ポリシーを適用。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// OAuth 2.0 Authorization Code Flow を使用。PKCE (RFC 7636) の実装を推奨。</para>
/// <para><b>OWASP A01:2021 - Broken Access Control</b>: 
/// アカウント連携・解除は認証済みユーザーのみ実行可能。他ユーザーのアカウントは操作不可。</para>
/// <para><b>Current Status</b>: OAuth フローのエンドポイントは現在未実装（501 Not Implemented）。</para>
/// </remarks>
public static class OAuthEndpoints
{
    /// <summary>
    /// OAuth エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // OAuth グループ: /api/v1/auth/oauth2 配下に全エンドポイントを配置
        // auth レート制限ポリシーを適用
        var group = app.MapGroup("/api/v1/auth/oauth2")
            .WithTags("OAuth")
            .RequireRateLimiting("auth");

        // GET /api/v1/auth/oauth2/authorization/{provider} - OAuth 認証フローを開始
        // 匿名アクセス許可: 未認証ユーザーがソーシャルログインを開始可能
        // 現在は未実装（501 Not Implemented を返却）
        group.MapGet("/authorization/{provider}", StartOAuthFlowAsync)
            .AllowAnonymous()
            .WithName("StartOAuthFlow")
            .Produces(501);

        // POST /api/v1/auth/oauth2/link/{provider} - 既存アカウントに OAuth プロバイダーを連携
        // 認証必須: ログイン済みユーザーのみアカウント連携可能
        // Authorization Code を受け取り、プロバイダーからユーザー情報を取得して連携
        group.MapPost("/link/{provider}", LinkAccountAsync)
            .RequireAuthorization()
            .WithName("LinkOAuthAccount")
            .Produces<MessageResponse>(200);

        // DELETE /api/v1/auth/oauth2/link/{provider} - OAuth プロバイダーとの連携を解除
        // 認証必須: 自身のアカウント連携のみ解除可能
        // 最後のログイン手段を失わないよう、パスワード設定済みか他の連携が残っていることを推奨
        group.MapDelete("/link/{provider}", UnlinkAccountAsync)
            .RequireAuthorization()
            .WithName("UnlinkOAuthAccount")
            .Produces(204);

        // GET /api/v1/auth/oauth2/accounts - 連携済み OAuth アカウント一覧を取得
        // 認証必須: 自身の連携済みアカウントのみ取得可能
        group.MapGet("/accounts", GetLinkedAccountsAsync)
            .RequireAuthorization()
            .WithName("GetLinkedOAuthAccounts")
            .Produces<IReadOnlyList<OAuthAccountDto>>(200);
    }

    /// <summary>
    /// 指定されたプロバイダーでの OAuth 認証フローを開始する（未実装）。
    /// </summary>
    /// <remarks>
    /// <para><b>Status</b>: 現在未実装。今後のアップデートで対応予定。</para>
    /// <para><b>Planned Flow</b>: 
    /// 1. state パラメータと PKCE code_verifier を生成してセッションに保存
    /// 2. プロバイダーの認証エンドポイントへリダイレクト
    /// 3. コールバックで Authorization Code を受け取り、トークンと交換
    /// 4. ユーザー情報を取得してログインまたはアカウント連携</para>
    /// <para><b>Supported Providers</b>: Google, Microsoft, GitHub（予定）</para>
    /// </remarks>
    private static async Task<IResult> StartOAuthFlowAsync(
        string provider,
        IOAuthService oAuthService,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        // 現在は未実装のため 501 Not Implemented を返却
        return Results.Problem(
            detail: "OAuth 認証フローは現在未実装です。今後のアップデートで対応予定です。",
            statusCode: 501,
            title: "Not Implemented");
    }

    /// <summary>
    /// 現在のユーザーアカウントに指定された OAuth プロバイダーを連携する。
    /// </summary>
    /// <remarks>
    /// <para><b>Authorization Code</b>: OAuth プロバイダーから取得した Authorization Code をクエリパラメータで受け取る。</para>
    /// <para><b>Token Exchange</b>: Code をアクセストークンと交換し、プロバイダーのユーザー情報を取得。</para>
    /// <para><b>Duplicate Check</b>: 同一プロバイダーのアカウントが既に他ユーザーに連携済みの場合はエラー。</para>
    /// <para><b>Security</b>: CSRF 防止のため、OAuth フロー開始時に発行した state パラメータの検証を推奨。</para>
    /// </remarks>
    private static async Task<IResult> LinkAccountAsync(
        string provider,
        [FromQuery] string code,
        ClaimsPrincipal user,
        IOAuthService oAuthService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await oAuthService.LinkAccountAsync(userId, provider, code, ct);
        return Results.Ok(new MessageResponse("アカウントを連携しました"));
    }

    /// <summary>
    /// 現在のユーザーアカウントから指定された OAuth プロバイダーとの連携を解除する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: 自身のアカウント連携のみ解除可能。他ユーザーの連携は操作不可。</para>
    /// <para><b>Login Method Check</b>: 最後のログイン手段（パスワードまたは OAuth）を失わないよう、
    /// 解除前に他のログイン手段が残っていることを確認することを推奨。</para>
    /// <para><b>Audit Log</b>: 連携解除操作はセキュリティログに記録される。</para>
    /// </remarks>
    private static async Task<IResult> UnlinkAccountAsync(
        string provider,
        ClaimsPrincipal user,
        IOAuthService oAuthService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await oAuthService.UnlinkAccountAsync(userId, provider, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// 現在のユーザーに連携されている OAuth アカウントの一覧を取得する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: 認証必須。自身の連携済みアカウントのみ取得可能（IDOR 防止）。</para>
    /// <para><b>Response</b>: プロバイダー名、連携日時、プロバイダー側のユーザー情報（メール等）を返却。</para>
    /// </remarks>
    private static async Task<IResult> GetLinkedAccountsAsync(
        ClaimsPrincipal user,
        IOAuthService oAuthService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await oAuthService.GetLinkedAccountsAsync(userId, ct));
    }
}
