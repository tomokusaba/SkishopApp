namespace AiSupportService.DTOs.Responses;

/// <summary>
/// チャットセッション情報のレスポンス DTO。
/// </summary>
/// <param name="Id">セッション ID。</param>
/// <param name="UserId">セッション所有ユーザー ID。</param>
/// <param name="Title">セッションタイトル（省略可）。</param>
/// <param name="Status">セッションの状態（ACTIVE / CLOSED 等）。</param>
/// <param name="CreatedAt">作成日時（UTC）。</param>
/// <param name="UpdatedAt">最終更新日時（UTC）。</param>
/// <param name="ClosedAt">クローズ日時（UTC、未クローズの場合は null）。</param>
public record ChatSessionResponse(
    string Id, string UserId, string? Title, string Status,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ClosedAt);

/// <summary>
/// チャットメッセージ情報のレスポンス DTO。
/// </summary>
/// <param name="Id">メッセージ ID。</param>
/// <param name="SessionId">所属するセッション ID。</param>
/// <param name="Role">メッセージの送信者ロール（user / assistant）。</param>
/// <param name="Content">メッセージ本文。</param>
/// <param name="TokenCount">消費トークン数（省略可）。</param>
/// <param name="CreatedAt">送信日時（UTC）。</param>
public record ChatMessageResponse(
    string Id, string SessionId, string Role, string Content,
    int? TokenCount, DateTime CreatedAt);

/// <summary>
/// AI 商品検索結果のレスポンス DTO。
/// </summary>
/// <param name="Query">実行された検索クエリ。</param>
/// <param name="TotalCount">ヒット件数。</param>
/// <param name="Items">検索結果アイテムのリスト。</param>
public record SearchResultResponse(
    string Query, int TotalCount, List<SearchResultItem> Items);

/// <summary>
/// 検索結果の個別アイテム。
/// </summary>
/// <param name="ProductId">商品 ID。</param>
/// <param name="Name">商品名。</param>
/// <param name="Price">価格。</param>
/// <param name="Category">カテゴリ（省略可）。</param>
/// <param name="ImageUrl">商品画像 URL（省略可）。</param>
/// <param name="RelevanceScore">検索クエリとの関連度スコア。</param>
public record SearchResultItem(
    string ProductId, string Name, decimal Price, string? Category,
    string? ImageUrl, double RelevanceScore);

/// <summary>
/// レコメンデーション結果のレスポンス DTO。
/// </summary>
/// <param name="Id">レコメンデーション ID。</param>
/// <param name="Type">レコメンデーション種別。</param>
/// <param name="ProductIds">推薦された商品 ID のリスト。</param>
/// <param name="Score">推薦スコア。</param>
/// <param name="Reason">推薦理由（省略可）。</param>
/// <param name="CreatedAt">生成日時（UTC）。</param>
public record RecommendationResponse(
    string Id, string Type, List<string> ProductIds, decimal Score,
    string? Reason, DateTime CreatedAt);

/// <summary>
/// 需要予測結果のレスポンス DTO。
/// </summary>
/// <param name="Id">予測 ID。</param>
/// <param name="ProductId">対象商品 ID。</param>
/// <param name="Sku">SKU コード（省略可）。</param>
/// <param name="ForecastDate">予測基準日。</param>
/// <param name="ForecastPeriod">予測期間（WEEKLY / MONTHLY 等）。</param>
/// <param name="PredictedDemand">予測需要数。</param>
/// <param name="ConfidenceScore">予測信頼度スコア。</param>
/// <param name="ModelVersion">使用したモデルバージョン（省略可）。</param>
/// <param name="CreatedAt">生成日時（UTC）。</param>
public record ForecastResponse(
    string Id, string ProductId, string? Sku, DateTime ForecastDate,
    string ForecastPeriod, int PredictedDemand, decimal ConfidenceScore,
    string? ModelVersion, DateTime CreatedAt);

/// <summary>
/// 検索分析レスポンス DTO。管理者向けの検索利用状況サマリ。
/// </summary>
/// <param name="TotalSearches">総検索回数。</param>
/// <param name="UniqueUsers">ユニークユーザー数。</param>
/// <param name="AvgResponseTimeMs">平均応答時間（ミリ秒）。</param>
/// <param name="TopQueries">上位検索クエリのリスト。</param>
/// <param name="SearchTypeDistribution">検索タイプ別の分布。</param>
public record SearchAnalyticsResponse(
    int TotalSearches, int UniqueUsers, double AvgResponseTimeMs,
    List<TopSearchQuery> TopQueries, Dictionary<string, int> SearchTypeDistribution);

/// <summary>
/// 上位検索クエリの集計結果。
/// </summary>
/// <param name="Query">検索クエリ文字列。</param>
/// <param name="Count">検索回数。</param>
public record TopSearchQuery(string Query, int Count);

/// <summary>
/// レコメンデーション分析レスポンス DTO。管理者向けのレコメンデーション利用状況サマリ。
/// </summary>
/// <param name="TotalRecommendations">総レコメンデーション数。</param>
/// <param name="ViewedCount">閲覧されたレコメンデーション数。</param>
/// <param name="ViewRate">閲覧率。</param>
/// <param name="TypeDistribution">レコメンデーションタイプ別の分布。</param>
public record RecommendationAnalyticsResponse(
    int TotalRecommendations, int ViewedCount, double ViewRate,
    Dictionary<string, int> TypeDistribution);

/// <summary>
/// チャット分析レスポンス DTO。管理者向けのチャット利用状況サマリ。
/// </summary>
/// <param name="TotalSessions">総セッション数。</param>
/// <param name="TotalMessages">総メッセージ数。</param>
/// <param name="AvgMessagesPerSession">セッションあたりの平均メッセージ数。</param>
/// <param name="EscalatedSessions">エスカレーションされたセッション数。</param>
/// <param name="StatusDistribution">セッション状態別の分布。</param>
public record ChatAnalyticsResponse(
    int TotalSessions, int TotalMessages, double AvgMessagesPerSession,
    int EscalatedSessions, Dictionary<string, int> StatusDistribution);

/// <summary>
/// モデルトレーニング状態のレスポンス DTO。
/// </summary>
/// <param name="Id">トレーニングジョブ ID。</param>
/// <param name="ModelName">モデル名。</param>
/// <param name="ModelVersion">モデルバージョン（省略可）。</param>
/// <param name="Status">トレーニング状態。</param>
/// <param name="StartedAt">開始日時（省略可）。</param>
/// <param name="CompletedAt">完了日時（省略可）。</param>
/// <param name="CreatedAt">作成日時（UTC）。</param>
public record ModelTrainingResponse(
    string Id, string ModelName, string? ModelVersion, string Status,
    DateTime? StartedAt, DateTime? CompletedAt, DateTime CreatedAt);
