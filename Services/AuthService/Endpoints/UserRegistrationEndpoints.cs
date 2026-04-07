using System.Security.Claims;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Endpoints;

/// <summary>
/// ユーザー登録・削除に関する API エンドポイントを提供するクラス。
/// 新規ユーザー登録、論理削除（退会）、物理削除（完全削除）機能を提供する。
/// </summary>
/// <remarks>
/// <para><b>Security</b>: 全エンドポイントに「general」レート制限ポリシーを適用。</para>
/// <para><b>OWASP A07:2021 - Identification and Authentication Failures</b>: 
/// パスワード要件（長さ、複雑さ）をバリデーションで強制。
/// メールアドレスの重複チェックを実施。</para>
/// <para><b>OWASP A01:2021 - Broken Access Control</b>: 
/// 論理削除は自身または管理者のみ実行可能。
/// 物理削除は管理者（AdminOnly ポリシー）のみ実行可能。</para>
/// <para><b>Data Protection</b>: 
/// 論理削除後も一定期間データを保持（法的要件、復旧対応）。
/// 物理削除は GDPR の「忘れられる権利」に対応。</para>
/// </remarks>
public static class UserRegistrationEndpoints
{
    /// <summary>
    /// ユーザー登録エンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    public static void MapUserRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        // ユーザー登録グループ: /api/v1/auth/users 配下に全エンドポイントを配置
        // general レート制限ポリシーを適用
        var group = app.MapGroup("/api/v1/auth/users")
            .WithTags("User Registration")
            .RequireRateLimiting("general");

        // POST /api/v1/auth/users - 新規ユーザー登録
        // 匿名アクセス許可: 誰でもアカウント作成可能
        // 登録後にメール確認メールを送信
        group.MapPost("/", RegisterAsync)
            .AllowAnonymous()
            .WithName("Register")
            .Produces<UserResponse>(201)
            .ProducesValidationProblem();

        // DELETE /api/v1/auth/users/{userId} - ユーザーの論理削除（退会）
        // 認証必須: 自身のアカウントまたは管理者のみ実行可能
        // 論理削除: deleted_at にタイムスタンプを設定し、ログイン不可に
        group.MapDelete("/{userId}", SoftDeleteAsync)
            .RequireAuthorization()
            .WithName("SoftDeleteUser")
            .Produces(204)
            .Produces(403);

        // DELETE /api/v1/auth/users/{userId}/hard - ユーザーの物理削除（完全削除）
        // 管理者専用（AdminOnly ポリシー）: データベースから完全に削除
        // GDPR「忘れられる権利」対応用のエンドポイント
        group.MapDelete("/{userId}/hard", HardDeleteAsync)
            .RequireAuthorization("AdminOnly")
            .WithName("HardDeleteUser")
            .Produces(204);
    }

    /// <summary>
    /// 新規ユーザーを登録し、確認メールを送信する。
    /// </summary>
    /// <remarks>
    /// <para><b>Input Validation</b>: メールアドレス形式、パスワード複雑さ要件を検証。</para>
    /// <para><b>Duplicate Check</b>: メールアドレスの重複をチェックし、既存の場合はエラー。</para>
    /// <para><b>Password Hashing</b>: パスワードは PBKDF2/Argon2 でハッシュ化して保存。</para>
    /// <para><b>Email Verification</b>: 登録完了後、確認メールを自動送信。
    /// メール確認完了までログイン不可とすることを推奨。</para>
    /// <para><b>Default Role</b>: 新規ユーザーにはデフォルトで USER ロールを付与。</para>
    /// <para><b>Audit Log</b>: ユーザー登録はセキュリティログに記録。</para>
    /// </remarks>
    private static async Task<IResult> RegisterAsync(
        [FromBody] UserCreateRequest request,
        IValidator<UserCreateRequest> validator,
        IUserRegistrationService registrationService,
        CancellationToken ct)
    {
        // FluentValidation による入力検証（メールアドレス形式、パスワード要件）
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // ユーザー登録処理（パスワードハッシュ化、確認メール送信を含む）
        var userResponse = await registrationService.RegisterAsync(request, ct);
        return Results.Created($"/api/v1/auth/users/{userResponse.Id}", userResponse);
    }

    /// <summary>
    /// ユーザーを論理削除（退会処理）する。
    /// </summary>
    /// <remarks>
    /// <para><b>Authorization</b>: 自身のアカウントまたは管理者（ADMIN ロール）のみ実行可能。</para>
    /// <para><b>IDOR Prevention</b>: ユーザー ID と JWT のユーザー ID を照合し、権限を検証。</para>
    /// <para><b>Soft Delete</b>: deleted_at にタイムスタンプを設定。データは保持されるがログイン不可。</para>
    /// <para><b>Session Invalidation</b>: 論理削除後、全ての既存セッションを無効化。</para>
    /// <para><b>Data Retention</b>: 法的要件に基づき、一定期間データを保持。
    /// 保持期間経過後にバッチ処理で物理削除することを推奨。</para>
    /// <para><b>Audit Log</b>: アカウント削除操作はセキュリティログに記録。</para>
    /// </remarks>
    private static async Task<IResult> SoftDeleteAsync(
        string userId,
        ClaimsPrincipal user,
        IUserRegistrationService registrationService,
        CancellationToken ct)
    {
        // JWT クレームからユーザー ID を取得
        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        
        // 権限チェック: 自身または管理者のみ削除可能
        if (currentUserId != userId && !user.IsInRole("ADMIN"))
            throw new ForbiddenException("他のユーザーを削除する権限がありません");

        await registrationService.SoftDeleteAsync(userId, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// ユーザーを物理削除（完全削除）する。管理者専用エンドポイント。
    /// </summary>
    /// <remarks>
    /// <para><b>Admin Only</b>: AdminOnly ポリシーにより、管理者のみ実行可能。</para>
    /// <para><b>GDPR Compliance</b>: 「忘れられる権利」（Right to be Forgotten）対応。
    /// ユーザーからの削除要求に基づき、全ての個人データを完全に削除。</para>
    /// <para><b>Cascade Delete</b>: 関連データ（注文履歴、セッション、MFA 設定等）も連鎖削除。
    /// 監査ログは法的要件に基づき保持することを推奨。</para>
    /// <para><b>Irreversible</b>: この操作は取り消し不可。実行前に確認プロセスを推奨。</para>
    /// <para><b>Audit Log</b>: 物理削除操作は管理者 ID と共にセキュリティログに記録。</para>
    /// </remarks>
    private static async Task<IResult> HardDeleteAsync(
        string userId,
        IUserRegistrationService registrationService,
        CancellationToken ct)
    {
        // 物理削除処理（関連データの連鎖削除を含む）
        await registrationService.HardDeleteAsync(userId, ct);
        return Results.NoContent();
    }
}
