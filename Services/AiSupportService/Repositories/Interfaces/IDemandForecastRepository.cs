using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing DemandForecast entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> DemandForecast
/// </para>
/// <para>
/// このリポジトリは AI による商品需要予測データを管理します。
/// 在庫管理サービスと連携し、適切な在庫水準の維持を支援します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>需要予測データの永続化</description></item>
///   <item><description>商品別の予測データ取得</description></item>
///   <item><description>予測精度分析のためのデータ提供</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>予測モデル:</strong>
/// 需要予測は機械学習モデル（Semantic Kernel 連携）により生成され、
/// 季節性、トレンド、イベント情報を考慮した予測値を提供します。
/// </para>
/// </remarks>
public interface IDemandForecastRepository
{
    /// <summary>
    /// 指定された商品の需要予測データを予測日の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 商品詳細画面での需要トレンド表示や在庫発注の判断に使用されます。
    /// 最新の予測から過去の予測まで時系列で取得できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>ForecastDate の降順でソートされる</description></item>
    ///   <item><description>存在しない商品 ID を指定した場合は空リストを返す</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="productId">商品 ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>需要予測データのリスト。</returns>
    Task<List<DemandForecast>> FindByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 全ての需要予測データを作成日時の降順で最大 100 件取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 管理画面での一覧表示や全体トレンド把握に使用されます。
    /// パフォーマンスのため最大 100 件に制限されています。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>最大 100 件までの取得制限がある</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>注意:</strong> 大量データの場合は <see cref="FindAllPagedAsync"/> を使用してください。
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>需要予測データのリスト（最大 100 件）。</returns>
    Task<List<DemandForecast>> FindAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 新しい需要予測データを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI モデルによる予測実行後に呼び出されます。
    /// 追加されたデータは <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>予測データを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="forecast">追加する需要予測データ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(DemandForecast forecast, CancellationToken ct = default);

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
    /// 全ての需要予測データをページネーション付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 大量の予測データを効率的に取得するためのページネーション対応メソッドです。
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
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>需要予測データリストと総件数のタプル。</returns>
    Task<(List<DemandForecast> Items, int TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 指定された商品の直近の需要予測データを取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 商品の最新予測トレンドを素早く確認するために使用されます。
    /// 在庫アラートや発注推奨の計算に活用されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされ、上位 limit 件を返す</description></item>
    ///   <item><description>limit のデフォルト値は 10</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="productId">商品 ID。</param>
    /// <param name="limit">取得する最大件数（デフォルト: 10）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>需要予測データのリスト。</returns>
    Task<List<DemandForecast>> FindByProductIdRecentAsync(string productId, int limit = 10, CancellationToken ct = default);
}
