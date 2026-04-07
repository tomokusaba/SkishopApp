using System.Security.Claims;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// 多要素認証 (MFA) に関する API エンドポイントを提供するクラス。
/// TOTP ベースの MFA セットアップ、検証、無効化機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「auth」レート制限ポリシーを適用。MFA コード検証は特に厳格に制限。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// MFA により認証の強度を向上。TOTP は RFC 6238 準拠の時間ベースワンタイムパスワード。</para>
/// <para><b>OWASP A02:2021 - Cryptographic Failures</b>: 
/// MFA シークレットは暗号化して保存。QR コードの Base32 エンコードされたシークレットは初回表示のみ。</para>
/// <para><b>Brute Force Prevention</b>: 
/// MFA コード検証の連続失敗時はセッショントークンを無効化し、再ログインを強制。</para>
/// </remarks>
public static class MfaEndpoints
{
    /// <summary>
    /// MFA エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapMfaEndpoints(this IEndpointRouteBuilder app)
    {
        // MFA グループ: /api/v1/auth/mfa 配下に全エンドポイントを配置
        // auth レート制限ポリシーを適用してブルートフォース攻撃を防止
        var group = app.MapGroup("/api/v1/auth/mfa")
            .WithTags("MFA")
            .RequireRateLimiting("auth");

        // POST /api/v1/auth/mfa/verify - MFA コードを検証して認証を完了
        // 匿名アクセス許可: ログインフロー中の MFA 検証ステップ
        // セッショントークンと TOTP コードの組み合わせで認証
        group.MapPost("/verify", VerifyMfaAsync)
            .AllowAnonymous()
            .WithName("VerifyMfa")
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem()
            .Produces(401);

        // POST /api/v1/auth/mfa/setup - MFA を有効化（QR コードとシークレットを取得）
        // 認証必須: ログイン済みユーザーのみ MFA をセットアップ可能
        // レスポンスには QR コード URI とリカバリコードを含む
        group.MapPost("/setup", SetupMfaAsync)
            .RequireAuthorization()
            .WithName("SetupMfa")
            .Produces<MfaSetupResponse>(200);

        // DELETE /api/v1/auth/mfa/disable - MFA を無効化
        // 認証必須: ログイン済みユーザーのみ自身の MFA を無効化可能
        // 無効化前にパスワード再確認を推奨（UI 側で実装）
        group.MapDelete("/disable", DisableMfaAsync)
            .RequireAuthorization()
            .WithName("DisableMfa")
            .Produces(204);
    }

    /// <summary>
    /// MFA コードを検証し、認証を完了して JWT トークンを発行する。
    /// </summary>
    /// <remarks>
    /// <para><b>Two-Step Authentication</b>: ログイン時に MFA が有効なユーザーは、
    /// まずセッショントークンを受け取り、このエンドポイントで TOTP コードを検証する。</para>
    /// <para><b>Session Token</b>: セッショントークンは短命（5分）で一度のみ使用可能。</para>
    /// <para><b>TOTP Validation</b>: 時間ベースのワンタイムパスワード検証。
    /// 30秒間隔で生成されるコードを検証。クロックスキューを考慮して前後1期間も許容。</para>
    /// <para><b>Brute Force Protection</b>: 5回連続失敗でセッショントークンを無効化。</para>
    /// </remarks>
    private static async Task<IResult> VerifyMfaAsync(
        [FromBody] MfaVerificationRequest request,
        IValidator<MfaVerificationRequest> validator,
        Services.Interfaces.IAuthService authService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（セッショントークン形式、コード形式等）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // セキュリティ監査用: クライアント情報を取得
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return Results.Ok(await authService.CompleteMfaLoginAsync(
            request.SessionToken, request.Code, ipAddress, userAgent, ct));
    }

    /// <summary>
    /// 現在のユーザーに対して MFA をセットアップし、QR コードとリカバリコードを返却する。
    /// </summary>
    /// <remarks>
    /// <para><b>TOTP Setup</b>: RFC 6238 準拠の TOTP シークレットを生成。</para>
    /// <para><b>QR Code</b>: Google Authenticator 等の認証アプリでスキャン可能な otpauth:// URI を返却。</para>
    /// <para><b>Recovery Codes</b>: デバイス紛失時用のリカバリコードを生成。
    /// リカバリコードは一度のみ表示され、以後は取得不可（セキュリティ上の理由）。</para>
    /// <para><b>Secret Storage</b>: シークレットは暗号化して DB に保存。</para>
    /// </remarks>
    private static async Task<IResult> SetupMfaAsync(
        ClaimsPrincipal user,
        IMfaService mfaService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await mfaService.SetupMfaAsync(userId, ct));
    }

    /// <summary>
    /// 現在のユーザーの MFA を無効化する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: MFA 無効化はセキュリティレベルを下げる操作のため、
    /// UI 側でパスワード再確認を実装することを推奨。</para>
    /// <para><b>Audit Log</b>: MFA 無効化操作はセキュリティログに記録される。</para>
    /// <para><b>Secret Deletion</b>: 無効化時に MFA シークレットとリカバリコードを削除。</para>
    /// </remarks>
    private static async Task<IResult> DisableMfaAsync(
        ClaimsPrincipal user,
        IMfaService mfaService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await mfaService.DisableMfaAsync(userId, ct);
        return Results.NoContent();
    }
}
