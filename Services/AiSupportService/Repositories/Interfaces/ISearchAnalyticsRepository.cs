using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing SearchAnalytics entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> SearchAnalytics
/// </para>
/// <para>
/// このリポジトリは AI 検索機能の利用分析データを管理します。
/// ユーザーの検索行動、検索パフォーマンス、人気クエリなどの
/// データを収集・分析し、サービス改善に活用します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>検索ログの永続化</description></item>
///   <item><description>期間指定での検索統計取得</description></item>
///   <item><description>人気検索クエリの分析</description></item>
///   <item><description>検索パフォーマンスメトリクスの集計</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>収集されるメトリクス:</strong>
/// <list type="bullet">
///   <item><description>検索クエリ（Query）</description></item>
///   <item><description>検索タイプ（SearchType: "product", "faq", "general"）</description></item>
///   <item><description>レスポンス時間（ResponseTimeMs）</description></item>
///   <item><description>結果件数（ResultCount）</description></item>
/// </list>
/// </para>
/// </remarks>
public interface ISearchAnalyticsRepository
{
    /// <summary>
    /// 新しい検索分析データを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索 API の呼び出し時に、検索クエリとレスポンス情報を記録します。
    /// 追加されたデータは <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>検索分析データを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    ///   <item><description>UserId は匿名検索の場合 null を許容する</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="analytics">追加する検索分析データ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(SearchAnalytics analytics, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の検索分析データを作成日時の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索ログの詳細分析やレポート生成に使用されます。
    /// 最新の検索から過去の検索まで時系列で確認できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>from ≤ CreatedAt ≤ to の範囲で取得する（両端を含む）</description></item>
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    ///   <item><description>日時は UTC として扱われる</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>注意:</strong> 大量データの場合は <see cref="FindByDateRangePagedAsync"/> を使用してください。
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索分析データのリスト。</returns>
    Task<List<SearchAnalytics>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーの検索分析データを作成日時の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーの検索履歴表示やパーソナライゼーションに使用されます。
    /// ユーザーの検索傾向を把握できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    ///   <item><description>該当するデータがない場合は空リストを返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索分析データのリスト。</returns>
    Task<List<SearchAnalytics>> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit of Work パターンに基づき、追跡中の全ての変更を
    /// 単一のトランザクションとしてデータベースにコミットします。
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">データベース更新時にエラーが発生した場合。</exception>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の検索件数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索利用量の統計に使用されます。
    /// 日次・週次・月次の検索回数を把握できます。
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索件数。</returns>
    Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内のユニークユーザー数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索機能のユニークユーザー数（DAU/MAU）の計算に使用されます。
    /// 匿名検索（UserId = null）はカウントに含まれません。
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ユニークユーザー数。</returns>
    Task<int> CountDistinctUsersByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の平均レスポンス時間（ミリ秒）を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索パフォーマンスのモニタリングに使用されます。
    /// SLA 遵守状況の確認やパフォーマンスボトルネックの検出に活用されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>ResponseTimeMs の平均値を計算する</description></item>
    ///   <item><description>データがない場合は 0.0 を返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>平均レスポンス時間（ミリ秒）。データがない場合は 0.0。</returns>
    Task<double> AverageResponseTimeByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の検索クエリを出現回数の降順で上位から取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 人気検索ワードの分析や検索サジェスト機能の改善に使用されます。
    /// トレンドキーワードの把握にも活用できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>クエリ文字列でグループ化し、出現回数をカウントする</description></item>
    ///   <item><description>出現回数の降順でソートする</description></item>
    ///   <item><description>上位 limit 件を返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="limit">取得する上位件数（デフォルト: 10）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>クエリ文字列と出現回数のタプルリスト。</returns>
    Task<List<(string Query, int Count)>> GetTopQueriesByDateRangeAsync(DateTime from, DateTime to, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の検索タイプ別件数分布を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索機能の利用傾向を分析するために使用されます。
    /// 各検索タイプの利用比率を把握できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>SearchType でグループ化し、件数をカウントする</description></item>
    ///   <item><description>該当するデータがないタイプは辞書に含まれない</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索タイプ文字列をキー、件数を値とする辞書。</returns>
    Task<Dictionary<string, int>> GetSearchTypeDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の検索分析データをページネーション付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 大量の検索ログを効率的に取得するためのページネーション対応メソッドです。
    /// 管理画面での一覧表示やエクスポート処理に使用されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>page は 1 始まり（page=1 で先頭ページ）</description></item>
    ///   <item><description>TotalCount は全件数を返す（ページ内件数ではない）</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索分析データリストと総件数のタプル。</returns>
    Task<(List<SearchAnalytics> Items, int TotalCount)> FindByDateRangePagedAsync(DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default);
}
