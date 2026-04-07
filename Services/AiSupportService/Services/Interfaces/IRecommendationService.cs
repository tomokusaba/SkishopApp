using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI レコメンデーション機能を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、ユーザーの行動履歴、嗜好、コンテキスト情報に基づいて
/// パーソナライズされた商品推薦を生成します。
/// </para>
/// <para>
/// レコメンデーションタイプ:
/// <list type="bullet">
///   <item><description>PERSONALIZED: ユーザーの嗜好・購買履歴に基づく推薦</description></item>
///   <item><description>SIMILAR: 特定商品に類似した商品の推薦</description></item>
///   <item><description>TRENDING: 全体の人気・トレンドに基づく推薦</description></item>
///   <item><description>SEASONAL: 季節・時期に応じた推薦</description></item>
///   <item><description>FREQUENTLY_BOUGHT_TOGETHER: 併売分析に基づく推薦</description></item>
/// </list>
/// </para>
/// <para>
/// キャッシュ戦略:
/// パフォーマンス向上のため、レコメンデーション結果は Redis にキャッシュされます。
/// キャッシュの有効期限はレコメンデーションタイプにより異なります
/// （パーソナライズ: 10 分、トレンド: 15 分、季節: 60 分など）。
/// </para>
/// </remarks>
public interface IRecommendationService
{
    /// <summary>
    /// ユーザーの購買履歴・嗜好に基づくパーソナライズレコメンデーションを取得する。
    /// </summary>
    /// <param name="userId">
    /// レコメンデーションを生成するユーザーの ID。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// パーソナライズされたレコメンデーションレスポンスのリスト。最大 10 件。
    /// </returns>
    /// <remarks>
    /// <para>
    /// スコア計算:
    /// ユーザープロファイルの嗜好（好みのカテゴリ、価格帯、ブランドなど）と
    /// 商品属性を照合し、0.0〜1.0 のスコアを算出します。
    /// </para>
    /// <para>
    /// 初回生成時には <c>ai.recommendation.generated</c> トピックにイベントが発行されます。
    /// 結果は 10 分間キャッシュされます。
    /// </para>
    /// </remarks>
    Task<List<RecommendationResponse>> GetPersonalizedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定商品に類似した商品のレコメンデーションを取得する。
    /// </summary>
    /// <param name="productId">
    /// 基準となる商品の ID。この商品に類似した商品が推薦されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 類似商品のレコメンデーションレスポンスのリスト。最大 5 件。
    /// 基準商品が見つからない場合は空のリスト。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 類似性は同一カテゴリ内の商品を対象に計算されます。
    /// 基準商品自体は結果から除外されます。
    /// </para>
    /// <para>
    /// 結果は 30 分間キャッシュされます。
    /// </para>
    /// </remarks>
    Task<List<RecommendationResponse>> GetSimilarProductsAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 現在のトレンド商品のレコメンデーションを取得する。
    /// </summary>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// トレンド商品のレコメンデーションレスポンスのリスト。最大 10 件の商品 ID を含む 1 件のレスポンス。
    /// </returns>
    /// <remarks>
    /// <para>
    /// トレンド商品は「人気 トレンド」クエリで検索された結果に基づきます。
    /// このメソッドはユーザー固有ではなく、全ユーザー共通の結果を返します。
    /// </para>
    /// <para>
    /// 結果は 15 分間キャッシュされます。
    /// </para>
    /// </remarks>
    Task<List<RecommendationResponse>> GetTrendingAsync(CancellationToken ct = default);

    /// <summary>
    /// 季節に応じたレコメンデーションを取得する。
    /// </summary>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 季節別レコメンデーションレスポンスのリスト。最大 10 件の商品 ID を含む 1 件のレスポンス。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 季節判定:
    /// <list type="bullet">
    ///   <item><description>10月〜3月: 冬（スキー関連）</description></item>
    ///   <item><description>4月〜6月: 春（アウトドア関連）</description></item>
    ///   <item><description>7月〜9月: 夏（トレーニング関連）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 結果は 60 分間キャッシュされます。
    /// </para>
    /// </remarks>
    Task<List<RecommendationResponse>> GetSeasonalAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定商品と一緒に購入されることが多い商品を取得する。
    /// </summary>
    /// <param name="productId">
    /// 基準となる商品の ID。この商品と併売される商品が推薦されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 併売商品のレコメンデーションレスポンスのリスト。最大 3 件。
    /// 基準商品が見つからない場合は空のリスト。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 現在の実装では、同一カテゴリ（またはアクセサリカテゴリ）の商品を推薦します。
    /// 将来的には、実際の購買データに基づく協調フィルタリングの実装が予定されています。
    /// </para>
    /// </remarks>
    Task<List<RecommendationResponse>> GetFrequentlyBoughtTogetherAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// レコメンデーションに対するユーザーフィードバックを記録する。
    /// </summary>
    /// <param name="userId">
    /// フィードバックを提供するユーザーの ID。
    /// </param>
    /// <param name="request">
    /// フィードバックリクエスト。レコメンデーション ID とフィードバックタイプ（CLICK, LIKE, DISMISS など）を含みます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// フィードバック記録操作を表すタスク。
    /// </returns>
    /// <exception cref="NotFoundException">
    /// 指定されたレコメンデーションが存在しない場合にスローされます。
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// 他のユーザーのパーソナライズレコメンデーションにフィードバックしようとした場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// フィードバックはレコメンデーションモデルの改善に使用されます。
    /// CLICK または LIKE フィードバックは、レコメンデーションが「閲覧済み」としてマークされます。
    /// </para>
    /// <para>
    /// 匿名ユーザー向けレコメンデーション（userId = "anonymous"）へのフィードバックは許可されます。
    /// </para>
    /// </remarks>
    Task RecordFeedbackAsync(string userId, RecommendationFeedbackRequest request, CancellationToken ct = default);
}
