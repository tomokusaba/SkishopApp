using System.Text.Json;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IRecommendationService"/> の実装。ユーザープロファイルと商品データに基づきレコメンデーションを生成する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、ユーザーの行動履歴、嗜好、コンテキスト情報に基づいて
/// パーソナライズされた商品推薦を生成します。
/// </para>
/// <para>
/// レコメンデーションタイプ:
/// <list type="bullet">
///   <item><description>PERSONALIZED: ユーザーの嗜好に基づく推薦（最大 10 件）</description></item>
///   <item><description>SIMILAR: 類似商品の推薦（最大 5 件）</description></item>
///   <item><description>TRENDING: トレンド商品の推薦（10 件）</description></item>
///   <item><description>SEASONAL: 季節商品の推薦（10 件）</description></item>
///   <item><description>FREQUENTLY_BOUGHT_TOGETHER: 併売商品の推薦（最大 3 件）</description></item>
/// </list>
/// </para>
/// <para>
/// キャッシュ戦略:
/// <list type="bullet">
///   <item><description>パーソナライズ: 10 分</description></item>
///   <item><description>類似商品: 30 分</description></item>
///   <item><description>トレンド: 15 分</description></item>
///   <item><description>季節: 60 分</description></item>
/// </list>
/// </para>
/// <para>
/// 匿名ユーザー向けレコメンデーション（trending, seasonal, similar, frequently-bought）は
/// <see cref="AnonymousUserId"/> を使用して生成・保存されます。
/// </para>
/// </remarks>
/// <param name="recommendationRepository">レコメンデーションリポジトリ。</param>
/// <param name="userProfileRepository">ユーザープロファイルリポジトリ。</param>
/// <param name="productClient">商品 API クライアント。</param>
/// <param name="outboxEventRepository">Outbox イベントリポジトリ。</param>
/// <param name="metrics">AI サポートメトリクス。</param>
/// <param name="logger">ロガー。</param>
/// <param name="cacheService">キャッシュサービス。</param>
public class RecommendationService(
    IRecommendationRepository recommendationRepository,
    IUserProfileRepository userProfileRepository,
    IProductClient productClient,
    IOutboxEventRepository outboxEventRepository,
    AiSupportMetrics metrics,
    ILogger<RecommendationService> logger,
    ICacheService cacheService) : IRecommendationService
{
    /// <summary>
    /// 匿名ユーザー（未ログインユーザー）向けレコメンデーションに使用されるユーザー ID。
    /// </summary>
    /// <remarks>
    /// trending、seasonal、similar、frequently-bought などの公開レコメンデーションは
    /// 特定のユーザーに紐付けられないため、この ID を使用して user_profiles テーブルと関連付けます。
    /// DB に対応するユーザープロファイルレコード（user_id = "anonymous"）が必要です。
    /// </remarks>
    private const string AnonymousUserId = "anonymous";

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 処理フロー:
    /// <list type="number">
    ///   <item><description>キャッシュをチェック（ヒットした場合は即座に返却）</description></item>
    ///   <item><description>既存のパーソナライズレコメンデーションをチェック</description></item>
    ///   <item><description>ユーザープロファイルから検索クエリを構築</description></item>
    ///   <item><description>商品検索 API を呼び出し</description></item>
    ///   <item><description>各商品のパーソナライズスコアを計算</description></item>
    ///   <item><description>レコメンデーションを生成・保存</description></item>
    ///   <item><description>Outbox イベントを発行（ai.recommendation.generated）</description></item>
    ///   <item><description>結果をキャッシュ（10 分）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<List<RecommendationResponse>> GetPersonalizedAsync(
        string userId, CancellationToken ct = default)
    {
        var cacheKey = $"recommendations:personalized:{userId}";
        var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var existing = await recommendationRepository.FindByUserIdAndTypeAsync(userId, "PERSONALIZED", ct);
        if (existing.Count > 0)
        {
            var existingResult = existing.Select(ToResponse).ToList();
            await cacheService.SetAsync(cacheKey, existingResult, TimeSpan.FromMinutes(10), ct);
            return existingResult;
        }

        var profile = await userProfileRepository.GetOrCreateAsync(userId, ct);

        var searchQuery = BuildPersonalizedSearchQuery(profile);
        var products = await productClient.SearchProductsAsync(searchQuery, ct: ct);

        if (products.Count == 0)
        {
            logger.LogWarning("パーソナライズ商品が見つかりませんでした: UserId={UserId}, Query={Query}", userId, searchQuery);
            return [];
        }

        var recommendations = new List<Recommendation>();
        foreach (var product in products.Take(10))
        {
            var score = CalculatePersonalizedScore(product, profile);
            var recommendation = Recommendation.CreatePersonalized(
                userId,
                [product.Id],
                score,
                $"あなたの好みに基づくおすすめ: {product.Name}");
            recommendations.Add(recommendation);
            await recommendationRepository.AddAsync(recommendation, ct);
        }

        if (recommendations.Count > 0)
        {
            try
            {
                var productIds = products.Take(10).Select(p => p.Id).ToList();
                var outboxEvent = new OutboxEvent
                {
                    AggregateType = "Recommendation",
                    AggregateId = recommendations[0].Id,
                    EventType = "RecommendationGenerated",
                    Topic = "ai.recommendation.generated",
                    Payload = JsonSerializer.Serialize(new
                    {
                        UserId = userId,
                        Type = "PERSONALIZED",
                        ProductIds = productIds,
                        Count = recommendations.Count,
                        OccurredAt = DateTime.UtcNow
                    })
                };
                await outboxEventRepository.AddAsync(outboxEvent, ct);
                await recommendationRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "パーソナライズレコメンデーションの保存に失敗しました: UserId={UserId}", userId);
            }
        }

        metrics.RecordRecommendation("PERSONALIZED");
        logger.LogInformation("パーソナライズレコメンデーション生成: UserId={UserId}, Count={Count}", userId, recommendations.Count);

        var result = recommendations.Select(ToResponse).ToList();
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), ct);
        return result;
    }

    /// <summary>
    /// ユーザーの嗜好情報から商品検索クエリを構築する。
    /// </summary>
    /// <param name="profile">ユーザープロファイル。</param>
    /// <returns>検索クエリ文字列。</returns>
    /// <remarks>
    /// <para>
    /// クエリ構築ロジック:
    /// <list type="number">
    ///   <item><description>PreferencesJson をパース</description></item>
    ///   <item><description>好みのカテゴリを最大 3 件抽出</description></item>
    ///   <item><description>好みのブランドを最大 2 件抽出</description></item>
    ///   <item><description>スキルレベルを抽出</description></item>
    ///   <item><description>これらを空白区切りで結合</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// PreferencesJson がない場合や解析に失敗した場合は、
    /// デフォルトクエリ "スキー 人気" を返します。
    /// </para>
    /// </remarks>
    private string BuildPersonalizedSearchQuery(UserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.PreferencesJson))
            return "スキー 人気";

        try
        {
            var preferences = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(profile.PreferencesJson);
            if (preferences is null)
                return "スキー 人気";

            var queryParts = new List<string>();

            if (preferences.TryGetValue("categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
            {
                foreach (var category in categories.EnumerateArray().Take(3))
                    queryParts.Add(category.GetString() ?? string.Empty);
            }

            if (preferences.TryGetValue("brands", out var brands) && brands.ValueKind == JsonValueKind.Array)
            {
                foreach (var brand in brands.EnumerateArray().Take(2))
                    queryParts.Add(brand.GetString() ?? string.Empty);
            }

            if (preferences.TryGetValue("skillLevel", out var skillLevel))
            {
                queryParts.Add(skillLevel.GetString() ?? string.Empty);
            }

            var query = string.Join(" ", queryParts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(query) ? "スキー 人気" : query;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "PreferencesJson パース失敗: UserId={UserId}", profile.UserId);
            return "スキー 人気";
        }
    }

    /// <summary>
    /// ユーザーの嗜好（カテゴリ一致・価格帯適合）に基づきパーソナライズスコアを計算する。
    /// </summary>
    /// <param name="product">評価対象の商品。</param>
    /// <param name="profile">ユーザープロファイル。</param>
    /// <returns>0.0〜1.0 の範囲のパーソナライズスコア。</returns>
    /// <remarks>
    /// <para>
    /// スコア計算ロジック:
    /// <list type="bullet">
    ///   <item><description>ベーススコア: 0.5</description></item>
    ///   <item><description>好みのカテゴリに一致: +0.3</description></item>
    ///   <item><description>好みの価格帯に収まる: +0.2</description></item>
    ///   <item><description>最大スコア: 1.0</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// PreferencesJson がない場合や解析に失敗した場合は、ベーススコア（0.5）を返します。
    /// </para>
    /// </remarks>
    private static decimal CalculatePersonalizedScore(ProductSearchResult product, UserProfile profile)
    {
        var baseScore = 0.5m;

        if (string.IsNullOrWhiteSpace(profile.PreferencesJson))
            return baseScore;

        try
        {
            var preferences = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(profile.PreferencesJson);
            if (preferences is null)
                return baseScore;

            if (preferences.TryGetValue("categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
            {
                var preferredCategories = categories.EnumerateArray()
                    .Select(c => c.GetString()?.ToLowerInvariant())
                    .Where(c => c is not null)
                    .ToList();

                if (product.Category is not null && preferredCategories.Contains(product.Category.ToLowerInvariant()))
                    baseScore += 0.3m;
            }

            if (preferences.TryGetValue("priceRange", out var priceRange) && priceRange.ValueKind == JsonValueKind.Object)
            {
                if (priceRange.TryGetProperty("min", out var minEl) && priceRange.TryGetProperty("max", out var maxEl))
                {
                    var min = minEl.GetDecimal();
                    var max = maxEl.GetDecimal();
                    if (product.Price >= min && product.Price <= max)
                        baseScore += 0.2m;
                }
            }

            return Math.Min(baseScore, 1.0m);
        }
        catch (JsonException)
        {
            // PreferencesJson パース失敗時はベーススコアを安全にフォールバック
            return baseScore;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 処理フロー:
    /// <list type="number">
    ///   <item><description>キャッシュをチェック（30 分有効期限）</description></item>
    ///   <item><description>基準商品の詳細を取得</description></item>
    ///   <item><description>同一カテゴリで商品を検索</description></item>
    ///   <item><description>基準商品を除外して最大 5 件を返却</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<List<RecommendationResponse>> GetSimilarProductsAsync(
        string productId, CancellationToken ct = default)
    {
        var cacheKey = $"recommendations:similar:{productId}";
        var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var product = await productClient.GetProductByIdAsync(productId, ct);
        if (product is null)
            return [];

        var similar = await productClient.SearchProductsAsync(product.Category ?? "スキー", ct: ct);
        var results = similar.Where(p => p.Id != productId).Take(5).ToList();

        var recommendations = results.Select(p =>
            Recommendation.CreateSimilar(AnonymousUserId, [p.Id], 0.75m, $"{product.Name}に似た商品")).ToList();

        if (recommendations.Count > 0)
        {
            try
            {
                await recommendationRepository.AddRangeAsync(recommendations, ct);
                await recommendationRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "類似商品レコメンデーションの保存に失敗しました: ProductId={ProductId}", productId);
            }
        }

        var result = recommendations.Select(ToResponse).ToList();
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), ct);
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 処理フロー:
    /// <list type="number">
    ///   <item><description>キャッシュをチェック（15 分有効期限）</description></item>
    ///   <item><description>"人気 トレンド" クエリで商品を検索</description></item>
    ///   <item><description>上位 10 件の商品 ID をレコメンデーションとして保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// このメソッドはユーザー固有ではなく、全ユーザー共通の結果を返します。
    /// </para>
    /// </remarks>
    public async Task<List<RecommendationResponse>> GetTrendingAsync(CancellationToken ct = default)
    {
        var cacheKey = "recommendations:trending";
        var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var products = await productClient.SearchProductsAsync("人気 トレンド", ct: ct);

        if (products.Count == 0)
        {
            logger.LogWarning("トレンド商品が見つかりませんでした");
            return [];
        }

        var productIds = products.Take(10).Select(p => p.Id).ToList();

        var recommendation = Recommendation.CreateTrending(AnonymousUserId, productIds, 0.9m, "現在人気のトレンド商品");

        try
        {
            await recommendationRepository.AddAsync(recommendation, ct);
            await recommendationRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "トレンドレコメンデーションの保存に失敗しました");
        }

        var result = new List<RecommendationResponse> { ToResponse(recommendation) };
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), ct);
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 季節判定ロジック:
    /// <list type="bullet">
    ///   <item><description>10月〜3月: 冬（"冬 スキー" で検索）</description></item>
    ///   <item><description>4月〜6月: 春（"春 アウトドア" で検索）</description></item>
    ///   <item><description>7月〜9月: 夏（"夏 トレーニング" で検索）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 結果は 60 分間キャッシュされます（季節は頻繁に変わらないため）。
    /// </para>
    /// </remarks>
    public async Task<List<RecommendationResponse>> GetSeasonalAsync(CancellationToken ct = default)
    {
        var cacheKey = "recommendations:seasonal";
        var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var season = DateTime.UtcNow.Month switch
        {
            >= 10 or <= 3 => "冬 スキー",
            >= 4 and <= 6 => "春 アウトドア",
            _ => "夏 トレーニング"
        };

        var products = await productClient.SearchProductsAsync(season, ct: ct);

        if (products.Count == 0)
        {
            logger.LogWarning("季節商品が見つかりませんでした: Season={Season}", season);
            return [];
        }

        var productIds = products.Take(10).Select(p => p.Id).ToList();

        var recommendation = Recommendation.CreateSeasonal(AnonymousUserId, productIds, 0.8m, "今シーズンのおすすめ商品");

        try
        {
            await recommendationRepository.AddAsync(recommendation, ct);
            await recommendationRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "季節レコメンデーションの保存に失敗しました: Season={Season}", season);
        }

        var result = new List<RecommendationResponse> { ToResponse(recommendation) };
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60), ct);
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 処理フロー:
    /// <list type="number">
    ///   <item><description>基準商品の詳細を取得</description></item>
    ///   <item><description>同一カテゴリ（またはアクセサリカテゴリ）で商品を検索</description></item>
    ///   <item><description>基準商品を除外して最大 3 件を返却</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 現在の実装ではカテゴリベースの推薦を行っています。
    /// 将来的には、実際の購買データに基づく協調フィルタリングの導入が予定されています。
    /// </para>
    /// </remarks>
    public async Task<List<RecommendationResponse>> GetFrequentlyBoughtTogetherAsync(
        string productId, CancellationToken ct = default)
    {
        var product = await productClient.GetProductByIdAsync(productId, ct);
        if (product is null)
            return [];

        var related = await productClient.SearchProductsAsync(product.Category ?? "アクセサリー", ct: ct);
        var results = related.Where(p => p.Id != productId).Take(3).ToList();

        var recommendations = results.Select(p =>
            Recommendation.CreateFrequentlyBoughtTogether(AnonymousUserId, [p.Id], 0.7m,
                "この商品と一緒に購入されています")).ToList();

        if (recommendations.Count > 0)
        {
            try
            {
                await recommendationRepository.AddRangeAsync(recommendations, ct);
                await recommendationRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "併売レコメンデーションの保存に失敗しました: ProductId={ProductId}", productId);
            }
        }

        return recommendations.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    /// <exception cref="NotFoundException">
    /// 指定されたレコメンデーションが存在しない場合にスローされます。
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// 他のユーザーのパーソナライズレコメンデーションにフィードバックしようとした場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// CLICK または LIKE フィードバックを受けた場合、
    /// レコメンデーションは「閲覧済み」としてマークされます。
    /// </para>
    /// <para>
    /// 匿名ユーザー向けレコメンデーション（userId = "anonymous"）へのフィードバックは
    /// 全ユーザーに許可されます。
    /// </para>
    /// </remarks>
    public async Task RecordFeedbackAsync(
        string userId, RecommendationFeedbackRequest request, CancellationToken ct = default)
    {
        var recommendation = await recommendationRepository.FindByIdAsync(request.RecommendationId, ct)
            ?? throw new NotFoundException($"レコメンデーション {request.RecommendationId} が見つかりません");

        if (recommendation.UserId != AnonymousUserId && recommendation.UserId != userId)
            throw new ForbiddenException("他のユーザーのレコメンデーションにはフィードバックできません");

        if (request.FeedbackType is "CLICK" or "LIKE")
            recommendation.MarkViewed();

        try
        {
            await recommendationRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "フィードバック保存に失敗しました: RecommendationId={RecommendationId}", request.RecommendationId);
            throw new BusinessException("フィードバックの保存に失敗しました。再度お試しください。");
        }

        logger.LogInformation(
            "レコメンデーションフィードバック: RecommendationId={RecommendationId}, FeedbackType={FeedbackType}",
            request.RecommendationId, request.FeedbackType);
    }

    /// <summary>
    /// <see cref="Recommendation"/> エンティティをレスポンス DTO に変換する。
    /// </summary>
    /// <param name="r">変換元のレコメンデーションエンティティ。</param>
    /// <returns>レコメンデーションレスポンス DTO。</returns>
    private static RecommendationResponse ToResponse(Recommendation r)
    {
        var productIds = JsonSerializer.Deserialize<List<string>>(r.ProductIdsJson) ?? [];
        return new RecommendationResponse(r.Id, r.Type, productIds, r.Score, r.Reason, r.CreatedAt);
    }
}
