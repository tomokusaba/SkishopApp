using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing Recommendation entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> Recommendation
/// </para>
/// <para>
/// このリポジトリは AI ベースの商品レコメンデーションデータを管理します。
/// ユーザーの行動履歴、購買パターン、嗜好に基づいた
/// パーソナライズされた商品推薦を提供します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>レコメンデーションの永続化</description></item>
///   <item><description>ユーザー別レコメンデーションの取得</description></item>
///   <item><description>レコメンデーション効果の分析（閲覧率、クリック率）</description></item>
///   <item><description>タイプ別の統計情報取得</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>レコメンデーションタイプ:</strong>
/// <list type="bullet">
///   <item><description>SIMILAR: 類似商品推薦</description></item>
///   <item><description>COMPLEMENTARY: 関連商品推薦</description></item>
///   <item><description>TRENDING: トレンド商品推薦</description></item>
///   <item><description>PERSONAL: パーソナライズ推薦</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IRecommendationRepository
{
    /// <summary>
    /// 指定されたユーザーのレコメンデーション一覧を作成日時の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーへの商品推薦表示に使用されます。
    /// 最新のレコメンデーションが先頭に表示されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>全てのタイプのレコメンデーションを含む</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    ///   <item><description>該当するレコメンデーションがない場合は空リストを返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レコメンデーションのリスト。</returns>
    Task<List<Recommendation>> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーおよびタイプのレコメンデーション一覧をスコア降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 特定のコンテキスト（商品詳細ページの類似商品など）に適した
    /// レコメンデーションを取得するために使用されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>Score の降順でソートされる（推薦度の高い順）</description></item>
    ///   <item><description>指定されたタイプのみをフィルタする</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="type">レコメンデーションタイプ（例: "SIMILAR", "COMPLEMENTARY"）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レコメンデーションのリスト。</returns>
    Task<List<Recommendation>> FindByUserIdAndTypeAsync(string userId, string type, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内に作成されたレコメンデーション一覧を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 管理者向けの分析やレポート生成に使用されます。
    /// レコメンデーション生成の傾向分析が可能です。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>from ≤ CreatedAt ≤ to の範囲で取得する（両端を含む）</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    ///   <item><description>日時は UTC として扱われる</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レコメンデーションのリスト。</returns>
    Task<List<Recommendation>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された ID のレコメンデーションを取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// レコメンデーション詳細の表示やステータス更新（閲覧フラグなど）に使用されます。
    /// 変更追跡が有効な状態で取得するため、更新操作が可能です。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しない ID を指定した場合は null を返す</description></item>
    ///   <item><description>変更追跡が有効（更新可能な状態で取得）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="id">レコメンデーション ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はレコメンデーション、見つからない場合は null。</returns>
    Task<Recommendation?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 新しいレコメンデーションを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI モデルによるレコメンデーション生成後に呼び出されます。
    /// 追加されたレコメンデーションは <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>レコメンデーションを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="recommendation">追加するレコメンデーション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(Recommendation recommendation, CancellationToken ct = default);

    /// <summary>
    /// 複数のレコメンデーションを一括追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// バッチ処理でのレコメンデーション生成時に使用されます。
    /// 個別追加より効率的に大量のレコメンデーションを登録できます。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 大量のレコメンデーションを追加する場合、このメソッドを使用することで
    /// データベースへのラウンドトリップを削減できます。
    /// </para>
    /// </remarks>
    /// <param name="recommendations">追加するレコメンデーションのコレクション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddRangeAsync(IEnumerable<Recommendation> recommendations, CancellationToken ct = default);

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
    /// 指定された期間内のレコメンデーション数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// レコメンデーション生成量の統計に使用されます。
    /// KPI ダッシュボードやレポートで活用されます。
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レコメンデーション件数。</returns>
    Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内の閲覧済みレコメンデーション数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// レコメンデーションの閲覧率（CTR）計算に使用されます。
    /// IsViewed = true のレコメンデーションをカウントします。
    /// </para>
    /// <para>
    /// <strong>閲覧率の計算:</strong>
    /// CTR = CountViewedByDateRangeAsync / CountByDateRangeAsync × 100
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>閲覧済みレコメンデーション件数。</returns>
    Task<int> CountViewedByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内のレコメンデーションタイプ別件数分布を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// タイプ別のレコメンデーション生成傾向を分析するために使用されます。
    /// 各タイプの効果測定やリソース配分の最適化に活用できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>期間内の全タイプについて件数を集計する</description></item>
    ///   <item><description>該当するレコメンデーションがないタイプは辞書に含まれない</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>タイプ文字列をキー、件数を値とする辞書。</returns>
    Task<Dictionary<string, int>> GetTypeDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーのレコメンデーションをページネーション付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 大量のレコメンデーション履歴を持つユーザー向けに、
    /// ページ単位での効率的な取得を提供します。
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
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レコメンデーションリストと総件数のタプル。</returns>
    Task<(List<Recommendation> Items, int TotalCount)> FindByUserIdPagedAsync(string userId, int page, int pageSize, CancellationToken ct = default);
}
