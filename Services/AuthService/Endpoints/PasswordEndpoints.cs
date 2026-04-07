using System.Security.Claims;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// パスワード管理に関する API エンドポイントを提供するクラス。
/// パスワードリセット要求、リセット確認、パスワード変更機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「auth」レート制限ポリシーを適用。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// パスワードリセットトークンは暗号学的に安全なランダム値を使用し、短い有効期限を設定。
/// パスワード要件（長さ、複雑さ）をバリデーションで強制。</para>
/// <para><b>OWASP A02:2021 - Cryptographic Failures</b>: 
/// パスワードは PBKDF2/Argon2 でハッシュ化して保存。平文パスワードはログに出力しない。</para>
/// <para><b>Email Enumeration Prevention</b>: 
/// パスワードリセット要求時、メールアドレスの存在有無に関わらず同一のレスポンスを返却。</para>
/// </remarks>
public static class PasswordEndpoints
{
    /// <summary>
    /// パスワード管理エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapPasswordEndpoints(this IEndpointRouteBuilder app)
    {
        // パスワード管理グループ: /api/v1/auth/password 配下に全エンドポイントを配置
        // auth レート制限ポリシーを適用してブルートフォース攻撃を防止
        var group = app.MapGroup("/api/v1/auth/password")
            .WithTags("Password")
            .RequireRateLimiting("auth");

        // POST /api/v1/auth/password/reset - パスワードリセットメールを送信
        // 匿名アクセス許可: パスワードを忘れたユーザー向け
        // メールアドレスの存在有無に関わらず同一のレスポンスを返却（Email Enumeration 防止）
        group.MapPost("/reset", RequestPasswordResetAsync)
            .AllowAnonymous()
            .WithName("RequestPasswordReset")
            .Produces<MessageResponse>(200)
            .ProducesValidationProblem();

        // POST /api/v1/auth/password/confirm - リセットトークンで新しいパスワードを設定
        // 匿名アクセス許可: パスワードリセットメールのリンクからアクセス
        // トークンは一度のみ有効（使用後は無効化）
        group.MapPost("/confirm", ConfirmPasswordResetAsync)
            .AllowAnonymous()
            .WithName("ConfirmPasswordReset")
            .Produces<MessageResponse>(200)
            .ProducesValidationProblem();

        // PUT /api/v1/auth/password/change - ログイン中のユーザーがパスワードを変更
        // 認証必須: 現在のパスワードを確認してから新しいパスワードに変更
        // 変更後は全セッションを無効化して再ログインを強制することを推奨
        group.MapPut("/change", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword")
            .Produces<MessageResponse>(200)
            .ProducesValidationProblem();
    }

    /// <summary>
    /// 指定されたメールアドレスにパスワードリセットメールを送信する。
    /// </summary>
    /// <remarks>
    /// <para><b>Email Enumeration Prevention</b>: メールアドレスが存在しない場合も、
    /// 存在する場合と同一のレスポンスを返却。攻撃者にメールアドレスの存在を推測させない。</para>
    /// <para><b>Token Security</b>: リセットトークンは暗号学的に安全なランダム値で、有効期限は 1 時間。</para>
    /// <para><b>Rate Limiting</b>: 同一メールアドレスへの連続送信を制限。</para>
    /// <para><b>Audit Log</b>: パスワードリセット要求はセキュリティログに記録。</para>
    /// </remarks>
    private static async Task<IResult> RequestPasswordResetAsync(
        [FromBody] PasswordResetRequest request,
        IValidator<PasswordResetRequest> validator,
        IPasswordService passwordService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（メールアドレス形式）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // リセットメール送信処理（メールアドレスが存在しなくても例外をスローしない）
        await passwordService.RequestResetAsync(request.Email, ct);
        return Results.Ok(new MessageResponse("パスワードリセットメールを送信しました"));
    }

    /// <summary>
    /// パスワードリセットトークンを検証し、新しいパスワードを設定する。
    /// </summary>
    /// <remarks>
    /// <para><b>Token Validation</b>: トークンの有効期限と使用済みフラグを検証。</para>
    /// <para><b>One-Time Use</b>: トークンは一度のみ有効。使用後は即座に無効化。</para>
    /// <para><b>Password Requirements</b>: 新しいパスワードは複雑さ要件を満たす必要がある
    /// （8文字以上、大文字・小文字・数字・記号を含む）。</para>
    /// <para><b>Session Invalidation</b>: パスワードリセット後、全ての既存セッションを無効化。</para>
    /// <para><b>Notification</b>: パスワード変更完了通知メールを送信。</para>
    /// </remarks>
    private static async Task<IResult> ConfirmPasswordResetAsync(
        [FromBody] PasswordResetConfirmRequest request,
        IValidator<PasswordResetConfirmRequest> validator,
        IPasswordService passwordService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（トークン形式、パスワード要件）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // トークン検証とパスワードリセット処理
        await passwordService.ConfirmResetAsync(request.Token, request.NewPassword, ct);
        return Results.Ok(new MessageResponse("パスワードをリセットしました"));
    }

    /// <summary>
    /// ログイン中のユーザーのパスワードを変更する。
    /// </summary>
    /// <remarks>
    /// <para><b>Current Password Verification</b>: 現在のパスワードを検証してから変更を許可。
    /// これにより、セッションハイジャック攻撃時のパスワード変更を防止。</para>
    /// <para><b>Password Requirements</b>: 新しいパスワードは複雑さ要件を満たす必要がある。</para>
    /// <para><b>Password History</b>: 過去に使用したパスワードの再使用を禁止することを推奨。</para>
    /// <para><b>Session Management</b>: パスワード変更後、オプションで全セッションを無効化可能。</para>
    /// <para><b>Notification</b>: パスワード変更完了通知メールを送信。</para>
    /// </remarks>
    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] PasswordChangeRequest request,
        IValidator<PasswordChangeRequest> validator,
        ClaimsPrincipal user,
        IPasswordService passwordService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（現在のパスワード、新しいパスワード要件）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // JWT クレームからユーザー ID を取得
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await passwordService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        return Results.Ok(new MessageResponse("パスワードを変更しました"));
    }
}
