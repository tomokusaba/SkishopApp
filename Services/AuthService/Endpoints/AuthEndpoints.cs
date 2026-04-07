using System.Security.Claims;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// 認証関連の API エンドポイントを提供するクラス。
/// ログイン、ログアウト、トークンリフレッシュ、トークン検証、ユーザー情報取得機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「auth」レート制限ポリシーを適用。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// ブルートフォース攻撃防止のため、認証系エンドポイントには厳格なレート制限を適用。
/// ログイン失敗時はアカウントロックアウトメカニズムが発動する可能性がある。</para>
/// <para><b>OWASP A02:2021 - Cryptographic Failures</b>: 
/// JWT トークンは適切な有効期限とセキュアな署名アルゴリズムで保護。
/// リフレッシュトークンは DB で管理し、使用後は無効化される。</para>
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>
    /// 認証エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // 認証グループ: /api/v1/auth 配下に全エンドポイントを配置
        // auth レート制限ポリシーを適用してブルートフォース攻撃を防止
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth");

        // POST /api/v1/auth/login - ユーザー認証とJWTトークン発行
        // 匿名アクセス許可（未認証ユーザー向け）
        // MFA 有効時は MfaRequired フラグとセッショントークンを返却
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem()
            .Produces(401);

        // POST /api/v1/auth/refresh - リフレッシュトークンで新規アクセストークンを発行
        // 匿名アクセス許可（リフレッシュトークン自体が認証の役割を持つ）
        // トークンローテーション: 使用済みリフレッシュトークンは即時無効化
        group.MapPost("/refresh", RefreshTokenAsync)
            .AllowAnonymous()
            .WithName("RefreshToken")
            .Produces<TokenRefreshResponse>(200)
            .ProducesValidationProblem()
            .Produces(401);

        // POST /api/v1/auth/logout - 現在のセッションを無効化
        // 認証必須: 有効な JWT トークンが必要
        // セッション ID をクレームから取得してサーバーサイドで無効化
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .Produces(204);

        // POST /api/v1/auth/validate - JWT トークンの有効性を検証
        // 認証必須: トークンが有効な場合のみアクセス可能
        // クライアント側でのトークン検証用（フロントエンド初期化時等）
        group.MapPost("/validate", ValidateTokenAsync)
            .RequireAuthorization()
            .WithName("ValidateToken")
            .Produces<TokenValidationResponse>(200);

        // GET /api/v1/auth/me - 現在のログインユーザー情報を取得
        // 認証必須: JWT トークンからユーザー ID を抽出して情報を返却
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<UserInfoResponse>(200);
    }

    /// <summary>
    /// ユーザー認証を実行し、JWT アクセストークンとリフレッシュトークンを発行する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: IP アドレスと User-Agent をセキュリティログに記録。</para>
    /// <para><b>MFA</b>: MFA 有効ユーザーの場合、MfaRequired=true と一時セッショントークンを返却。
    /// クライアントは /api/v1/auth/mfa/verify で MFA コードを検証する必要がある。</para>
    /// <para><b>Rate Limiting</b>: 連続失敗時はアカウントロックアウトが発動する可能性がある。</para>
    /// </remarks>
    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IValidator<LoginRequest> validator,
        Services.Interfaces.IAuthService authService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（メールアドレス形式、パスワード要件等）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // セキュリティ監査用: クライアント情報を取得
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return Results.Ok(await authService.LoginAsync(request, ipAddress, userAgent, ct));
    }

    /// <summary>
    /// リフレッシュトークンを使用して新しいアクセストークンを発行する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: トークンローテーション実装。使用済みリフレッシュトークンは即時無効化。</para>
    /// <para><b>OWASP</b>: リフレッシュトークンの再利用検出時は全セッションを強制ログアウト。</para>
    /// </remarks>
    private static async Task<IResult> RefreshTokenAsync(
        [FromBody] TokenRefreshRequest request,
        IValidator<TokenRefreshRequest> validator,
        Services.Interfaces.IAuthService authService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await authService.RefreshTokenAsync(request, ct));
    }

    /// <summary>
    /// 現在のセッションからログアウトし、リフレッシュトークンを無効化する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: サーバーサイドでセッションを無効化。クライアント側のトークン削除も必要。</para>
    /// </remarks>
    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal user,
        Services.Interfaces.IAuthService authService,
        CancellationToken ct)
    {
        // JWT クレームからセッション ID を取得
        var sessionId = user.FindFirstValue("session_id")
            ?? throw new UnauthorizedException();
        await authService.LogoutAsync(sessionId, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// 現在の JWT トークンの有効性を検証し、トークン情報を返却する。
    /// </summary>
    /// <remarks>
    /// <para><b>Usage</b>: フロントエンド初期化時やページ遷移時のトークン検証に使用。</para>
    /// </remarks>
    private static async Task<IResult> ValidateTokenAsync(
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        // JWT クレームからユーザー情報を抽出
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user.FindFirstValue(ClaimTypes.Role);
        var expClaim = user.FindFirstValue("exp");
        DateTimeOffset? expiresAt = expClaim is not null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim))
            : null;

        return Results.Ok(new TokenValidationResponse(true, userId, role, expiresAt));
    }

    /// <summary>
    /// 現在ログイン中のユーザーの詳細情報を取得する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: 認証必須。JWT トークンから抽出したユーザー ID に基づいて情報を返却。</para>
    /// <para><b>IDOR Prevention</b>: 自身の情報のみ取得可能（他ユーザーの情報は取得不可）。</para>
    /// </remarks>
    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal user,
        Services.Interfaces.IAuthService authService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await authService.GetCurrentUserAsync(userId, ct));
    }
}
