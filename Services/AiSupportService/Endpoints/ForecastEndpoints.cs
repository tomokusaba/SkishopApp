using System.Security.Claims;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Services.Interfaces;
using FluentValidation;

namespace AiSupportService.Endpoints;

/// <summary>
/// 需要予測機能の Minimal API エンドポイントを定義する（管理者専用）。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは商品の需要予測機能を提供します。
/// 管理者は特定商品の需要予測を生成し、過去の予測結果を参照できます。
/// 予測結果は在庫管理や仕入れ計画に活用されます。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/admin/ai/forecast
/// </para>
/// <para>
/// <b>Authentication:</b> Required (AdminOnly ポリシー)
/// </para>
/// <para>
/// <b>Rate Limiting:</b> admin-api ポリシー適用
/// </para>
/// </remarks>
public static class ForecastEndpoints
{
    /// <summary>
    /// 需要予測関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>POST /generate</term>
    ///     <description>新規需要予測を生成</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /{productId}</term>
    ///     <description>特定商品の需要予測履歴を取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /</term>
    ///     <description>全商品の需要予測一覧を取得</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapForecastEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ai/forecast")
            .RequireRateLimiting("admin-api")
            .WithTags("Forecasts")
            .RequireAuthorization("AdminOnly");

        // POST /api/v1/admin/ai/forecast/generate
        // 新規需要予測を生成する。
        // Request: GenerateForecastRequest (JSON body)
        // Response: ForecastResponse (201 Created) + Location ヘッダー
        // Error: 400 Validation Problem, 401 Unauthorized
        group.MapPost("/generate", GenerateForecast)
            .WithName("GenerateForecast")
            .Produces<ForecastResponse>(201)
            .ProducesValidationProblem()
            .ProducesProblem(401);

        // GET /api/v1/admin/ai/forecast/{productId}
        // 特定商品の需要予測履歴を取得する。
        // Request: productId (path), page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<ForecastResponse> (200 OK)
        // Error: 401 Unauthorized
        group.MapGet("/{productId}", GetByProduct)
            .WithName("GetForecastByProduct")
            .Produces<List<ForecastResponse>>(200)
            .ProducesProblem(401);

        // GET /api/v1/admin/ai/forecast
        // 全商品の需要予測一覧を取得する。
        // Request: page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<ForecastResponse> (200 OK)
        // Error: 401 Unauthorized
        group.MapGet("/", GetAll)
            .WithName("GetAllForecasts")
            .Produces<List<ForecastResponse>>(200)
            .ProducesProblem(401);
    }

    /// <summary>
    /// 新規需要予測を生成する。
    /// </summary>
    /// <param name="request">予測生成リクエスト（対象商品、予測期間等）。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="service">需要予測サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>生成された需要予測（201 Created）。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> GenerateForecast(
        GenerateForecastRequest request,
        IValidator<GenerateForecastRequest> validator,
        ClaimsPrincipal user,
        IForecastService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var result = await service.GenerateAsync(request, adminUserId, ct);
        return Results.Created($"/api/v1/admin/ai/forecast/{result.ProductId}", result);
    }

    /// <summary>
    /// 特定商品の需要予測履歴を取得する。
    /// </summary>
    /// <param name="productId">対象商品 ID。</param>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 20）。</param>
    /// <param name="service">需要予測サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされた需要予測履歴。</returns>
    private static async Task<IResult> GetByProduct(
        string productId,
        int page,
        int pageSize,
        IForecastService service,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var all = await service.GetByProductIdAsync(productId, ct);
        var totalCount = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Results.Ok(new PagedResult<ForecastResponse>(
            paged, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)));
    }

    /// <summary>
    /// 全商品の需要予測一覧を取得する。
    /// </summary>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 20）。</param>
    /// <param name="service">需要予測サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされた全需要予測一覧。</returns>
    private static async Task<IResult> GetAll(
        int page,
        int pageSize,
        IForecastService service,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var all = await service.GetAllAsync(ct);
        var totalCount = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Results.Ok(new PagedResult<ForecastResponse>(
            paged, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)));
    }
}
