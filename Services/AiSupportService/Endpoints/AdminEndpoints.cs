using System.Security.Claims;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Endpoints;

/// <summary>
/// AI モデル管理の Minimal API エンドポイントを定義する（管理者専用）。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは AI モデルのトレーニング状態管理と新規トレーニング開始機能を提供します。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/admin/ai/models
/// </para>
/// <para>
/// <b>Authentication:</b> Required (AdminOnly ポリシー)
/// </para>
/// <para>
/// <b>Rate Limiting:</b> admin-api ポリシー適用
/// </para>
/// </remarks>
public static class AdminEndpoints
{
    /// <summary>
    /// モデル管理関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>GET /</term>
    ///     <description>全モデルのトレーニング状態一覧を取得（ページネーション対応）</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /{modelName}/train</term>
    ///     <description>指定モデルの新規トレーニングを開始</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ai/models")
            .WithTags("Models")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("admin-api");

        // GET /api/v1/admin/ai/models
        // 全モデルのトレーニング状態をページネーション付きで取得する。
        // Request: page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<ModelTrainingResponse> (200 OK)
        // Error: 401 Unauthorized（認証なし/権限不足）
        group.MapGet("/", GetModels)
            .WithName("GetModels")
            .Produces<List<ModelTrainingResponse>>(200)
            .ProducesProblem(401);

        // POST /api/v1/admin/ai/models/{modelName}/train
        // 指定されたモデル名で新規トレーニングジョブを開始する。
        // Request: modelName (string) パスパラメータ
        // Response: ModelTrainingResponse (201 Created) + Location ヘッダー
        // Error: 401 Unauthorized（認証なし/権限不足）
        group.MapPost("/{modelName}/train", TrainModel)
            .WithName("TrainModel")
            .Produces<ModelTrainingResponse>(201)
            .ProducesProblem(401);
    }

    /// <summary>
    /// 全モデルのトレーニング状態一覧を取得する。
    /// </summary>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 20）。</param>
    /// <param name="service">モデルトレーニングサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされたモデルトレーニング状態のリスト。</returns>
    private static async Task<IResult> GetModels(
        int page,
        int pageSize,
        IModelTrainingService service,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var all = await service.GetAllAsync(ct);
        var totalCount = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Results.Ok(new PagedResult<ModelTrainingResponse>(
            paged, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)));
    }

    /// <summary>
    /// 指定モデルの新規トレーニングを開始する。
    /// </summary>
    /// <param name="modelName">トレーニングを開始するモデル名。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="service">モデルトレーニングサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>作成されたトレーニングジョブの詳細（201 Created）。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> TrainModel(
        string modelName,
        ClaimsPrincipal user,
        IModelTrainingService service,
        CancellationToken ct)
    {
        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var result = await service.StartTrainingAsync(modelName, adminUserId, ct);
        return Results.Created($"/api/v1/admin/ai/models/{modelName}", result);
    }
}
