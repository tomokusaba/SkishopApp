using AiSupportService.DTOs.Responses;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI 機能（検索・レコメンデーション・チャット）の利用分析データを提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、AI サポート機能の利用状況を分析するためのメトリクスを提供します。
/// 管理者向けダッシュボードやレポート生成に使用されることを想定しています。
/// </para>
/// <para>
/// 分析データは以下の 3 つのカテゴリに分類されます：
/// <list type="bullet">
///   <item><description>検索分析: 検索クエリ、レスポンス時間、検索タイプの分布</description></item>
///   <item><description>レコメンデーション分析: 推薦数、閲覧率、推薦タイプの分布</description></item>
///   <item><description>チャット分析: セッション数、メッセージ数、エスカレーション率</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IAiAnalyticsService
{
    /// <summary>
    /// 指定期間の検索分析データを取得する。
    /// </summary>
    /// <param name="from">
    /// 集計開始日時（UTC）。<c>null</c> の場合は過去 30 日を起点とします。
    /// </param>
    /// <param name="to">
    /// 集計終了日時（UTC）。<c>null</c> の場合は現在日時を使用します。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 検索分析レスポンス。検索総数、ユニークユーザー数、平均レスポンス時間、
    /// 人気クエリ上位 10 件、検索タイプ分布を含みます。
    /// </returns>
    /// <remarks>
    /// 検索分析データには、キーワード検索と AI 支援検索の両方が含まれます。
    /// レスポンス時間はミリ秒単位で計測されます。
    /// </remarks>
    Task<SearchAnalyticsResponse> GetSearchAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>
    /// 指定期間のレコメンデーション分析データを取得する。
    /// </summary>
    /// <param name="from">
    /// 集計開始日時（UTC）。<c>null</c> の場合は過去 30 日を起点とします。
    /// </param>
    /// <param name="to">
    /// 集計終了日時（UTC）。<c>null</c> の場合は現在日時を使用します。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// レコメンデーション分析レスポンス。生成総数、閲覧数、閲覧率、
    /// レコメンデーションタイプ（PERSONALIZED、SIMILAR、TRENDING 等）の分布を含みます。
    /// </returns>
    /// <remarks>
    /// 閲覧率（View Rate）は、生成されたレコメンデーションのうち
    /// ユーザーが実際にクリックまたは閲覧した割合を示します。
    /// </remarks>
    Task<RecommendationAnalyticsResponse> GetRecommendationAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>
    /// 指定期間のチャット分析データを取得する。
    /// </summary>
    /// <param name="from">
    /// 集計開始日時（UTC）。<c>null</c> の場合は過去 30 日を起点とします。
    /// </param>
    /// <param name="to">
    /// 集計終了日時（UTC）。<c>null</c> の場合は現在日時を使用します。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// チャット分析レスポンス。セッション総数、メッセージ総数、
    /// セッションあたりの平均メッセージ数、エスカレーション数、ステータス分布を含みます。
    /// </returns>
    /// <remarks>
    /// エスカレーション数は、AI チャットから有人対応に切り替えられたセッションの数を示します。
    /// この値が高い場合、AI の回答品質や対応範囲の見直しが必要な可能性があります。
    /// </remarks>
    Task<ChatAnalyticsResponse> GetChatAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}
