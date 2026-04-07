using System.Security.Claims;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Services.Interfaces;
using FluentValidation;

namespace AiSupportService.Endpoints;

/// <summary>
/// レコメンデーション機能の Minimal API エンドポイントを定義する。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは AI ベースの商品レコメンデーション機能を提供します。
/// パーソナライズドレコメンデーション、類似商品、トレンド商品、季節商品、
/// 頻繁に一緒に購入される商品の各種レコメンデーションを取得できます。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/ai/recommendations
/// </para>
/// <para>
/// <b>Authentication:</b> 
/// - Required: /personalized, /feedback（ユーザー固有データへのアクセス）
/// - AllowAnonymous: /similar, /trending, /seasonal, /frequently-bought（公開データ）
/// </para>
/// <para>
/// <b>Rate Limiting:</b> recommendation-api ポリシー適用
/// </para>
/// </remarks>
public static class RecommendationEndpoints
{
    /// <summary>
    /// レコメンデーション関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>GET /personalized</term>
    ///     <description>ユーザーの購入履歴・閲覧履歴に基づくパーソナライズドレコメンデーションを取得（要認証）</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /similar/{productId}</term>
    ///     <description>指定商品に類似した商品を取得（公開）</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /trending</term>
    ///     <description>現在のトレンド商品を取得（公開）</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /seasonal</term>
    ///     <description>現在の季節に適した商品を取得（公開）</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /frequently-bought/{productId}</term>
    ///     <description>指定商品と一緒に購入されることが多い商品を取得（公開）</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /feedback</term>
    ///     <description>レコメンデーションに対するユーザーフィードバックを記録（要認証）</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/recommendations")
            .RequireRateLimiting("recommendation-api")
            .WithTags("Recommendations");

        // GET /api/v1/ai/recommendations/personalized
        // ユーザーの購入履歴・閲覧履歴に基づくパーソナライズドレコメンデーションを取得する。
        // Request: page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<RecommendationResponse> (200 OK)
        // Error: 401 Unauthorized（認証必須）
        group.MapGet("/personalized", GetPersonalized)
            .RequireAuthorization()
            .WithName("GetPersonalized")
            .Produces<List<RecommendationResponse>>(200)
            .ProducesProblem(401);

        // GET /api/v1/ai/recommendations/similar/{productId}
        // 指定商品に類似した商品を取得する（協調フィルタリング）。
        // Request: productId (path)
        // Response: List<RecommendationResponse> (200 OK)
        group.MapGet("/similar/{productId}", GetSimilar)
            .AllowAnonymous()
            .WithName("GetSimilar")
            .Produces<List<RecommendationResponse>>(200);

        // GET /api/v1/ai/recommendations/trending
        // 現在のトレンド商品を取得する。
        // Response: List<RecommendationResponse> (200 OK)
        group.MapGet("/trending", GetTrending)
            .AllowAnonymous()
            .WithName("GetTrending")
            .Produces<List<RecommendationResponse>>(200);

        // GET /api/v1/ai/recommendations/seasonal
        // 現在の季節に適した商品を取得する。
        // Response: List<RecommendationResponse> (200 OK)
        group.MapGet("/seasonal", GetSeasonal)
            .AllowAnonymous()
            .WithName("GetSeasonal")
            .Produces<List<RecommendationResponse>>(200);

        // GET /api/v1/ai/recommendations/frequently-bought/{productId}
        // 指定商品と一緒に購入されることが多い商品を取得する（マーケットバスケット分析）。
        // Request: productId (path)
        // Response: List<RecommendationResponse> (200 OK)
        group.MapGet("/frequently-bought/{productId}", GetFrequentlyBought)
            .AllowAnonymous()
            .WithName("GetFrequentlyBought")
            .Produces<List<RecommendationResponse>>(200);

        // POST /api/v1/ai/recommendations/feedback
        // レコメンデーションに対するユーザーフィードバックを記録する。
        // Request: RecommendationFeedbackRequest (JSON body)
        // Response: 204 No Content
        // Error: 400 Validation Problem, 401 Unauthorized
        group.MapPost("/feedback", RecordFeedback)
            .RequireAuthorization()
            .WithName("RecommendationFeedback")
            .Produces(204)
            .ProducesValidationProblem()
            .ProducesProblem(401);
    }

    /// <summary>
    /// パーソナライズドレコメンデーションを取得する。
    /// </summary>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 20）。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされたパーソナライズドレコメンデーション。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> GetPersonalized(
        int page,
        int pageSize,
        ClaimsPrincipal user,
        IRecommendationService service,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var all = await service.GetPersonalizedAsync(userId, ct);
        var totalCount = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Results.Ok(new PagedResult<RecommendationResponse>(
            paged, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)));
    }

    /// <summary>
    /// 指定商品に類似した商品を取得する。
    /// </summary>
    /// <param name="productId">基準となる商品 ID。</param>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>類似商品のリスト。</returns>
    private static async Task<IResult> GetSimilar(
        string productId,
        IRecommendationService service,
        CancellationToken ct)
        => Results.Ok(await service.GetSimilarProductsAsync(productId, ct));

    /// <summary>
    /// 現在のトレンド商品を取得する。
    /// </summary>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>トレンド商品のリスト。</returns>
    private static async Task<IResult> GetTrending(
        IRecommendationService service,
        CancellationToken ct)
        => Results.Ok(await service.GetTrendingAsync(ct));

    /// <summary>
    /// 現在の季節に適した商品を取得する。
    /// </summary>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>季節商品のリスト。</returns>
    private static async Task<IResult> GetSeasonal(
        IRecommendationService service,
        CancellationToken ct)
        => Results.Ok(await service.GetSeasonalAsync(ct));

    /// <summary>
    /// 指定商品と一緒に購入されることが多い商品を取得する。
    /// </summary>
    /// <param name="productId">基準となる商品 ID。</param>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>頻繁に一緒に購入される商品のリスト。</returns>
    private static async Task<IResult> GetFrequentlyBought(
        string productId,
        IRecommendationService service,
        CancellationToken ct)
        => Results.Ok(await service.GetFrequentlyBoughtTogetherAsync(productId, ct));

    /// <summary>
    /// レコメンデーションに対するユーザーフィードバックを記録する。
    /// </summary>
    /// <param name="request">フィードバックリクエスト（クリック、購入等のアクション）。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="service">レコメンデーションサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>204 No Content。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> RecordFeedback(
        RecommendationFeedbackRequest request,
        IValidator<RecommendationFeedbackRequest> validator,
        ClaimsPrincipal user,
        IRecommendationService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await service.RecordFeedbackAsync(userId, request, ct);
        return Results.NoContent();
    }
}
