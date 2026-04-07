using System.Security.Claims;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AiSupportService.Endpoints;

/// <summary>
/// AI 検索機能の Minimal API エンドポイントを定義する。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは AI ベースのセマンティック検索機能を提供します。
/// 自然言語クエリによる商品検索、オートコンプリートサジェスチョン、
/// 検索フィードバック機能を含みます。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/ai/search
/// </para>
/// <para>
/// <b>Authentication:</b> 
/// - Required: /feedback（ユーザーフィードバックの記録）
/// - AllowAnonymous: /, /suggest（検索・サジェスチョンは公開）
/// </para>
/// <para>
/// <b>Rate Limiting:</b> search-api ポリシー適用
/// </para>
/// </remarks>
public static class SearchEndpoints
{
    /// <summary>
    /// 検索関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>GET /</term>
    ///     <description>AI ベースの商品検索を実行（公開）</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /feedback</term>
    ///     <description>検索結果に対するユーザーフィードバックを記録（要認証）</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /suggest</term>
    ///     <description>オートコンプリートサジェスチョンを取得（公開）</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/search")
            .RequireRateLimiting("search-api")
            .WithTags("Search");

        // GET /api/v1/ai/search
        // AI ベースの商品検索を実行する（セマンティック検索）。
        // Request: SearchRequest クエリパラメータ（query, category, minPrice, maxPrice, page, pageSize 等）
        // Response: SearchResultResponse (200 OK)
        // Error: 400 Validation Problem
        group.MapGet("/", Search)
            .AllowAnonymous()
            .WithName("SearchProducts")
            .Produces<SearchResultResponse>(200)
            .ProducesValidationProblem();

        // POST /api/v1/ai/search/feedback
        // 検索結果に対するユーザーフィードバックを記録する。
        // Request: SearchFeedbackRequest (JSON body)
        // Response: 204 No Content
        // Error: 400 Validation Problem, 401 Unauthorized
        group.MapPost("/feedback", RecordFeedback)
            .RequireAuthorization()
            .WithName("SearchFeedback")
            .Produces(204)
            .ProducesValidationProblem()
            .ProducesProblem(401);

        // GET /api/v1/ai/search/suggest
        // オートコンプリートサジェスチョンを取得する。
        // Request: query (string) クエリパラメータ
        // Response: List<string> (200 OK)
        // Error: 400 Validation Problem（クエリが不正な場合）
        group.MapGet("/suggest", GetSuggestions)
            .AllowAnonymous()
            .WithName("SearchSuggestions")
            .Produces<List<string>>(200);
    }

    /// <summary>
    /// AI ベースの商品検索を実行する。
    /// </summary>
    /// <param name="request">検索リクエスト（クエリ、フィルタ条件、ページネーション等）。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal（任意、パーソナライズに使用）。</param>
    /// <param name="searchService">検索サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>検索結果とファセット情報。</returns>
    private static async Task<IResult> Search(
        [AsParameters] SearchRequest request,
        IValidator<SearchRequest> validator,
        ClaimsPrincipal? user,
        ISearchService searchService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Results.Ok(await searchService.SearchAsync(request, userId, ct));
    }

    /// <summary>
    /// 検索結果に対するユーザーフィードバックを記録する。
    /// </summary>
    /// <param name="request">フィードバックリクエスト（検索 ID、クリック位置等）。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="searchService">検索サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>204 No Content。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> RecordFeedback(
        [FromBody] SearchFeedbackRequest request,
        IValidator<SearchFeedbackRequest> validator,
        ClaimsPrincipal user,
        ISearchService searchService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await searchService.RecordFeedbackAsync(request, userId, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// オートコンプリートサジェスチョンを取得する。
    /// </summary>
    /// <param name="query">検索クエリの一部（1〜200文字）。</param>
    /// <param name="searchService">検索サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>サジェスチョン候補のリスト。</returns>
    private static async Task<IResult> GetSuggestions(
        [FromQuery] string query,
        ISearchService searchService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["query"] = ["検索クエリは1〜200文字で入力してください"]
            });

        return Results.Ok(await searchService.GetSuggestionsAsync(query, ct));
    }
}
