using AiSupportService.DTOs.Responses;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Endpoints;

/// <summary>
/// AI 分析レポートの Minimal API エンドポイントを定義する（管理者専用）。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは AI 機能の利用状況分析レポートを提供します。
/// 検索、レコメンデーション、チャットの各機能の分析データを取得できます。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/admin/ai/analytics
/// </para>
/// <para>
/// <b>Authentication:</b> Required (AdminOnly ポリシー)
/// </para>
/// <para>
/// <b>Rate Limiting:</b> admin-api ポリシー適用
/// </para>
/// </remarks>
public static class AnalyticsEndpoints
{
    /// <summary>
    /// 分析関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>GET /search</term>
    ///     <description>検索機能の分析レポートを取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /recommendations</term>
    ///     <description>レコメンデーション機能の分析レポートを取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /chat</term>
    ///     <description>チャット機能の分析レポートを取得</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ai/analytics")
            .RequireRateLimiting("admin-api")
            .WithTags("Analytics")
            .RequireAuthorization("AdminOnly");

        // GET /api/v1/admin/ai/analytics/search
        // 検索機能の分析レポートを取得する。
        // Request: from (DateTime?), to (DateTime?) クエリパラメータ
        // Response: SearchAnalyticsResponse (200 OK)
        // Error: 401 Unauthorized（認証なし/権限不足）
        group.MapGet("/search", GetSearchAnalytics)
            .WithName("GetSearchAnalytics")
            .Produces<SearchAnalyticsResponse>(200)
            .ProducesProblem(401);

        // GET /api/v1/admin/ai/analytics/recommendations
        // レコメンデーション機能の分析レポートを取得する。
        // Request: from (DateTime?), to (DateTime?) クエリパラメータ
        // Response: RecommendationAnalyticsResponse (200 OK)
        // Error: 401 Unauthorized（認証なし/権限不足）
        group.MapGet("/recommendations", GetRecommendationAnalytics)
            .WithName("GetRecommendationAnalytics")
            .Produces<RecommendationAnalyticsResponse>(200)
            .ProducesProblem(401);

        // GET /api/v1/admin/ai/analytics/chat
        // チャット機能の分析レポートを取得する。
        // Request: from (DateTime?), to (DateTime?) クエリパラメータ
        // Response: ChatAnalyticsResponse (200 OK)
        // Error: 401 Unauthorized（認証なし/権限不足）
        group.MapGet("/chat", GetChatAnalytics)
            .WithName("GetChatAnalytics")
            .Produces<ChatAnalyticsResponse>(200)
            .ProducesProblem(401);
    }

    /// <summary>
    /// 日付範囲パラメータのバリデーションを行う。
    /// </summary>
    /// <param name="from">開始日時。</param>
    /// <param name="to">終了日時。</param>
    /// <returns>バリデーションエラーがある場合は <see cref="IResult"/>、なければ null。</returns>
    private static IResult? ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue)
        {
            if (from.Value > to.Value)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["from"] = ["from は to より前の日付を指定してください"]
                });
            if ((to.Value - from.Value).TotalDays > 365)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["to"] = ["日付範囲は最大365日までです"]
                });
        }
        return null;
    }

    /// <summary>
    /// 検索機能の分析レポートを取得する。
    /// </summary>
    /// <param name="from">分析期間の開始日時（任意）。</param>
    /// <param name="to">分析期間の終了日時（任意）。</param>
    /// <param name="service">AI 分析サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>検索分析レポート。</returns>
    private static async Task<IResult> GetSearchAnalytics(
        DateTime? from,
        DateTime? to,
        IAiAnalyticsService service,
        CancellationToken ct)
    {
        var validationError = ValidateDateRange(from, to);
        if (validationError is not null) return validationError;
        return Results.Ok(await service.GetSearchAnalyticsAsync(from, to, ct));
    }

    /// <summary>
    /// レコメンデーション機能の分析レポートを取得する。
    /// </summary>
    /// <param name="from">分析期間の開始日時（任意）。</param>
    /// <param name="to">分析期間の終了日時（任意）。</param>
    /// <param name="service">AI 分析サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>レコメンデーション分析レポート。</returns>
    private static async Task<IResult> GetRecommendationAnalytics(
        DateTime? from,
        DateTime? to,
        IAiAnalyticsService service,
        CancellationToken ct)
    {
        var validationError = ValidateDateRange(from, to);
        if (validationError is not null) return validationError;
        return Results.Ok(await service.GetRecommendationAnalyticsAsync(from, to, ct));
    }

    /// <summary>
    /// チャット機能の分析レポートを取得する。
    /// </summary>
    /// <param name="from">分析期間の開始日時（任意）。</param>
    /// <param name="to">分析期間の終了日時（任意）。</param>
    /// <param name="service">AI 分析サービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>チャット分析レポート。</returns>
    private static async Task<IResult> GetChatAnalytics(
        DateTime? from,
        DateTime? to,
        IAiAnalyticsService service,
        CancellationToken ct)
    {
        var validationError = ValidateDateRange(from, to);
        if (validationError is not null) return validationError;
        return Results.Ok(await service.GetChatAnalyticsAsync(from, to, ct));
    }
}
