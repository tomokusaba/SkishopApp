using FluentValidation;
using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MailSendService.Endpoints;

/// <summary>
/// メール管理用の Minimal API エンドポイント定義。
/// </summary>
/// <remarks>
/// メールログの参照・リトライ、テストメール送信、統計情報取得、
/// およびメールテンプレートの CRUD 操作を提供する。
/// 管理者向けエンドポイントは AdminOnly ポリシーで保護される。
/// </remarks>
public static class MailEndpoints
{
    /// <summary>
    /// メール管理用のエンドポイントをアプリケーションに登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー。</param>
    /// <remarks>
    /// <para>管理グループ（/admin/mail）: ログ参照、リトライ、テスト送信（AdminOnly）。</para>
    /// <para>統計エンドポイント（/admin/mail/stats）: AdminOrManager ポリシー。</para>
    /// <para>テンプレートグループ（/admin/mail/templates）: テンプレート CRUD（AdminOnly）。</para>
    /// </remarks>
    public static void MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/admin/mail")
            .WithTags("Mail Administration")
            .RequireAuthorization("AdminOnly")
            ;

        adminGroup.MapGet("/logs", GetMailLogs)
            .Produces<PaginatedResult<MailLogResponse>>()
            .WithName("GetMailLogs");
        adminGroup.MapGet("/logs/{id}", GetMailLogById)
            .Produces<MailLogResponse>()
            .Produces(404)
            .WithName("GetMailLogById");
        adminGroup.MapPost("/logs/{id}/retry", RetryMailSend)
            .Produces<MailLogResponse>(202)
            .Produces(404)
            .WithName("RetryMailSend");
        adminGroup.MapPost("/test", SendTestMail)
            .RequireRateLimiting("test-mail")
            .Produces<MailLogResponse>(201)
            .ProducesValidationProblem()
            .WithName("SendTestMail");

        // Stats endpoint uses AdminOrManager policy (overrides group-level AdminOnly)
        adminGroup.MapGet("/stats", GetMailStats)
            .RequireAuthorization("AdminOrManager")
            .Produces<MailStatsResponse>()
            .WithName("GetMailStats");

        var templateGroup = app.MapGroup("/admin/mail/templates")
            .WithTags("Mail Templates")
            .RequireAuthorization("AdminOnly")
            ;

        templateGroup.MapGet("/", GetAllTemplates)
            .Produces<PaginatedResult<MailTemplateResponse>>()
            .WithName("GetAllTemplates");
        templateGroup.MapGet("/{id}", GetTemplateById)
            .Produces<MailTemplateResponse>()
            .Produces(404)
            .WithName("GetTemplateById");
        templateGroup.MapPost("/", CreateTemplate)
            .Produces<MailTemplateResponse>(201)
            .ProducesValidationProblem()
            .WithName("CreateTemplate");
        templateGroup.MapPut("/{id}", UpdateTemplate)
            .Produces<MailTemplateResponse>()
            .ProducesValidationProblem()
            .Produces(404)
            .WithName("UpdateTemplate");
        templateGroup.MapDelete("/{id}", DeactivateTemplate)
            .Produces(204)
            .Produces(404)
            .WithName("DeactivateTemplate");
    }

    /// <summary>
    /// メールログ一覧をページネーション付きで取得する。
    /// </summary>
    /// <param name="query">ページネーションパラメータ。</param>
    /// <param name="mailService">メールサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログ一覧の HTTP 200 レスポンス。</returns>
    private static async Task<IResult> GetMailLogs(
        [AsParameters] MailLogQueryParams query,
        IMailService mailService,
        CancellationToken ct)
        => Results.Ok(await mailService.GetLogsAsync(query.Page, query.PageSize, ct));

    /// <summary>
    /// 指定 ID のメールログ詳細を取得する。
    /// </summary>
    /// <param name="id">メールログ ID。</param>
    /// <param name="mailService">メールサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログ詳細の HTTP 200 レスポンス、または ID 不正時の HTTP 400。</returns>
    private static async Task<IResult> GetMailLogById(
        string id,
        IMailService mailService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.Problem("ID は必須です", statusCode: 400);
        return Results.Ok(await mailService.GetLogByIdAsync(id, ct));
    }

    /// <summary>
    /// 指定 ID のメール送信をリトライする。
    /// </summary>
    /// <param name="id">リトライ対象のメールログ ID。</param>
    /// <param name="mailService">メールサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>リトライ結果の HTTP 202 レスポンス、または ID 不正時の HTTP 400。</returns>
    private static async Task<IResult> RetryMailSend(
        string id,
        IMailService mailService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.Problem("ID は必須です", statusCode: 400);
        return Results.Accepted(value: await mailService.RetryAsync(id, ct));
    }

    /// <summary>
    /// メール送信統計情報を取得する。
    /// </summary>
    /// <param name="mailService">メールサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>統計情報の HTTP 200 レスポンス。</returns>
    private static async Task<IResult> GetMailStats(
        IMailService mailService,
        CancellationToken ct)
        => Results.Ok(await mailService.GetStatsAsync(ct));

    /// <summary>
    /// テストメールを送信する。レート制限付き。
    /// </summary>
    /// <param name="request">テストメール送信リクエスト。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="mailService">メールサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信結果の HTTP 201 レスポンス、またはバリデーションエラー。</returns>
    private static async Task<IResult> SendTestMail(
        [FromBody] TestMailRequest request,
        IValidator<TestMailRequest> validator,
        IMailService mailService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        var result = await mailService.SendTestMailAsync(request, ct);
        return Results.Created($"/admin/mail/logs/{result.Id}", result);
    }

    /// <summary>
    /// 全メールテンプレートをページネーション付きで取得する。
    /// </summary>
    /// <param name="query">ページネーションパラメータ。</param>
    /// <param name="templateService">テンプレートサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート一覧の HTTP 200 レスポンス。</returns>
    private static async Task<IResult> GetAllTemplates(
        [AsParameters] TemplateQueryParams query,
        ITemplateService templateService,
        CancellationToken ct)
        => Results.Ok(await templateService.GetAllAsync(query.Page, query.PageSize, ct));

    /// <summary>
    /// 指定 ID のメールテンプレート詳細を取得する。
    /// </summary>
    /// <param name="id">テンプレート ID。</param>
    /// <param name="templateService">テンプレートサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート詳細の HTTP 200 レスポンス、または ID 不正時の HTTP 400。</returns>
    private static async Task<IResult> GetTemplateById(
        string id,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.Problem("ID は必須です", statusCode: 400);
        return Results.Ok(await templateService.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// 新しいメールテンプレートを作成する。
    /// </summary>
    /// <param name="request">テンプレート作成リクエスト。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="templateService">テンプレートサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>作成結果の HTTP 201 レスポンス、またはバリデーションエラー。</returns>
    private static async Task<IResult> CreateTemplate(
        [FromBody] TemplateCreateRequest request,
        IValidator<TemplateCreateRequest> validator,
        ITemplateService templateService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        var created = await templateService.CreateAsync(request, ct);
        return Results.Created($"/admin/mail/templates/{created.Id}", created);
    }

    /// <summary>
    /// 既存のメールテンプレートを更新する。
    /// </summary>
    /// <param name="id">更新対象のテンプレート ID。</param>
    /// <param name="request">テンプレート更新リクエスト。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="templateService">テンプレートサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>更新結果の HTTP 200 レスポンス、またはバリデーションエラー。</returns>
    private static async Task<IResult> UpdateTemplate(
        string id,
        [FromBody] TemplateUpdateRequest request,
        IValidator<TemplateUpdateRequest> validator,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.Problem("ID は必須です", statusCode: 400);
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await templateService.UpdateAsync(id, request, ct));
    }

    /// <summary>
    /// 指定 ID のメールテンプレートを無効化（論理削除）する。
    /// </summary>
    /// <param name="id">無効化対象のテンプレート ID。</param>
    /// <param name="templateService">テンプレートサービス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>HTTP 204 レスポンス、または ID 不正時の HTTP 400。</returns>
    private static async Task<IResult> DeactivateTemplate(
        string id,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.Problem("ID は必須です", statusCode: 400);
        await templateService.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}

/// <summary>
/// メールログ一覧取得用のクエリパラメータ。
/// </summary>
/// <param name="Page">ページ番号（最小値 1、デフォルト 1）。</param>
/// <param name="PageSize">1 ページあたりの件数（1〜100、デフォルト 20）。</param>
public record MailLogQueryParams(int Page = 1, int PageSize = 20)
{
    /// <summary>ページ番号。最小値 1 にクランプされる。</summary>
    public int Page { get; } = Math.Max(1, Page);
    /// <summary>1 ページあたりの件数。1〜100 の範囲にクランプされる。</summary>
    public int PageSize { get; } = Math.Clamp(PageSize, 1, 100);
}

/// <summary>
/// テンプレート一覧取得用のクエリパラメータ。
/// </summary>
/// <param name="Page">ページ番号（最小値 1、デフォルト 1）。</param>
/// <param name="PageSize">1 ページあたりの件数（1〜100、デフォルト 20）。</param>
public record TemplateQueryParams(int Page = 1, int PageSize = 20)
{
    /// <summary>ページ番号。最小値 1 にクランプされる。</summary>
    public int Page { get; } = Math.Max(1, Page);
    /// <summary>1 ページあたりの件数。1〜100 の範囲にクランプされる。</summary>
    public int PageSize { get; } = Math.Clamp(PageSize, 1, 100);
}
