using System.Security.Claims;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// メールアドレス確認に関する API エンドポイントを提供するクラス。
/// メール確認トークンの検証と確認メールの再送信機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「general」レート制限ポリシーを適用。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// メール確認トークンは暗号学的に安全なランダム値を使用し、有効期限を設定。
/// トークン再利用攻撃を防止するため、使用済みトークンは無効化される。</para>
/// <para><b>Email Enumeration Prevention</b>: 
/// 確認メール再送信はログインユーザーのみ可能とし、メールアドレスの推測攻撃を防止。</para>
/// </remarks>
public static class EmailVerificationEndpoints
{
    /// <summary>
    /// メール確認エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapEmailVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        // メール確認グループ: /api/v1/auth/email 配下に全エンドポイントを配置
        // general レート制限ポリシーを適用
        var group = app.MapGroup("/api/v1/auth/email")
            .WithTags("Email Verification")
            .RequireRateLimiting("general");

        // POST /api/v1/auth/email/verify - メール確認トークンを検証
        // 匿名アクセス許可: 確認メールのリンクからアクセスされるため
        // トークンは一度のみ有効（使用後は無効化）
        group.MapPost("/verify", VerifyEmailAsync)
            .AllowAnonymous()
            .WithName("VerifyEmail")
            .Produces<MessageResponse>(200)
            .ProducesValidationProblem();

        // POST /api/v1/auth/email/resend - 確認メールを再送信
        // 認証必須: 自身のメールアドレス宛てのみ再送信可能
        // 連続送信を防止するためレート制限を適用
        group.MapPost("/resend", ResendVerificationEmailAsync)
            .RequireAuthorization()
            .WithName("ResendVerificationEmail")
            .Produces<MessageResponse>(200);
    }

    /// <summary>
    /// メール確認トークンを検証し、ユーザーのメールアドレスを確認済みとしてマークする。
    /// </summary>
    /// <remarks>
    /// <para><b>Token Security</b>: トークンは暗号学的に安全なランダム値で、有効期限は 24 時間。</para>
    /// <para><b>One-Time Use</b>: トークンは一度のみ有効。使用後は即座に無効化される。</para>
    /// <para><b>User State</b>: 確認完了後、ユーザーの EmailVerified フラグが true に更新される。</para>
    /// </remarks>
    private static async Task<IResult> VerifyEmailAsync(
        [FromBody] EmailVerificationRequest request,
        IValidator<EmailVerificationRequest> validator,
        IUserRegistrationService registrationService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（トークン形式等）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // トークン検証とメールアドレス確認処理
        await registrationService.VerifyEmailAsync(request.Token, ct);
        return Results.Ok(new MessageResponse("メールアドレスを確認しました"));
    }

    /// <summary>
    /// 現在のログインユーザーに対して、メール確認メールを再送信する。
    /// </summary>
    /// <remarks>
    /// <para><b>Security</b>: 認証必須。自身のメールアドレス宛てのみ送信可能。</para>
    /// <para><b>Rate Limiting</b>: スパム防止のため、短時間での連続送信は制限される。</para>
    /// <para><b>Token Rotation</b>: 再送信時は新しいトークンを生成し、古いトークンは無効化。</para>
    /// </remarks>
    private static async Task<IResult> ResendVerificationEmailAsync(
        ClaimsPrincipal user,
        IUserRegistrationService registrationService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await registrationService.ResendVerificationEmailAsync(userId, ct);
        return Results.Ok(new MessageResponse("確認メールを再送信しました"));
    }
}
