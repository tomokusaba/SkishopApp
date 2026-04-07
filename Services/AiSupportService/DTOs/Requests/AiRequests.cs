using System.ComponentModel.DataAnnotations;

namespace AiSupportService.DTOs.Requests;

/// <summary>
/// チャットセッション作成リクエスト DTO。
/// </summary>
/// <param name="Title">セッションのタイトル（省略可）。</param>
public record CreateChatSessionRequest(
    [MaxLength(200)] string? Title = null);

/// <summary>
/// チャットメッセージ送信リクエスト DTO。
/// </summary>
/// <param name="Message">ユーザーが送信するメッセージ本文。</param>
public record SendMessageRequest(
    [Required, MinLength(1), MaxLength(4000)] string Message);

/// <summary>
/// AI 商品検索リクエスト DTO。
/// </summary>
/// <param name="Query">検索クエリ文字列。</param>
/// <param name="Category">絞り込み対象のカテゴリ（省略可）。</param>
/// <param name="MinPrice">最低価格フィルタ（省略可）。</param>
/// <param name="MaxPrice">最高価格フィルタ（省略可）。</param>
/// <param name="Page">取得するページ番号（1 始まり）。</param>
/// <param name="PageSize">1 ページあたりの件数。</param>
public record SearchRequest(
    [Required, MaxLength(500)] string Query,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// 検索結果に対するユーザーフィードバック（クリック等）リクエスト DTO。
/// </summary>
/// <param name="SearchId">フィードバック対象の検索 ID。</param>
/// <param name="ProductId">ユーザーが選択した商品 ID。</param>
public record SearchFeedbackRequest(
    [Required, MaxLength(36)] string SearchId,
    [Required, MaxLength(36)] string ProductId);

/// <summary>
/// レコメンデーション結果に対するフィードバックリクエスト DTO。
/// </summary>
/// <param name="RecommendationId">フィードバック対象のレコメンデーション ID。</param>
/// <param name="FeedbackType">フィードバック種別（CLICK / PURCHASE / DISMISS / LIKE / DISLIKE）。</param>
/// <param name="ProductId">対象商品 ID（省略可）。</param>
public record RecommendationFeedbackRequest(
    [Required, MaxLength(36)] string RecommendationId,
    [Required] string FeedbackType,
    [MaxLength(36)] string? ProductId = null);

/// <summary>
/// 需要予測生成リクエスト DTO。
/// </summary>
/// <param name="ProductId">予測対象の商品 ID。</param>
/// <param name="ForecastPeriod">予測期間（WEEKLY / MONTHLY / QUARTERLY / YEARLY）。</param>
/// <param name="Sku">商品の SKU コード（省略可）。</param>
public record GenerateForecastRequest(
    [Required, MaxLength(36)] string ProductId,
    [Required, MaxLength(20)] string ForecastPeriod,
    [MaxLength(50)] string? Sku = null);
