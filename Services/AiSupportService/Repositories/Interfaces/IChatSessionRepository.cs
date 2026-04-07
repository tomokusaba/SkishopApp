using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing ChatSession entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> ChatSession
/// </para>
/// <para>
/// このリポジトリはチャットセッションの Aggregate Root を管理します。
/// ChatSession は ChatMessage のコレクションを子エンティティとして持ち、
/// 会話全体のライフサイクルを管理します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>セッションの CRUD 操作</description></item>
///   <item><description>ユーザー単位でのセッション管理</description></item>
///   <item><description>セッション統計情報の集計</description></item>
///   <item><description>期間指定での分析データ取得</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>セッションステータス:</strong>
/// <list type="bullet">
///   <item><description>ACTIVE: 進行中のセッション</description></item>
///   <item><description>COMPLETED: 完了したセッション</description></item>
///   <item><description>ESCALATED: オペレーターにエスカレーション済み</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IChatSessionRepository
{
    /// <summary>
    /// 指定された ID のチャットセッションをメッセージ付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// セッション詳細の表示や会話の続行に使用されます。
    /// 関連するメッセージを Eager Loading で取得します。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しない ID を指定した場合は null を返す</description></item>
    ///   <item><description>Messages コレクションは CreatedAt 昇順でソートされる</description></item>
    ///   <item><description>変更追跡が有効（更新可能な状態で取得）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="id">セッション ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はセッション、見つからない場合は null。</returns>
    Task<ChatSession?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 指定された ID とユーザー ID に一致するチャットセッションをメッセージ付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// IDOR（Insecure Direct Object Reference）防止のため、
    /// セッション所有者の検証を含む取得を行います。
    /// ユーザーは自身のセッションのみアクセス可能です。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>ID とユーザー ID の両方が一致する場合のみセッションを返す</description></item>
    ///   <item><description>他ユーザーのセッション ID を指定した場合は null を返す</description></item>
    ///   <item><description>Messages コレクションは CreatedAt 昇順でソートされる</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="id">セッション ID。</param>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はセッション、見つからない場合は null。</returns>
    Task<ChatSession?> FindByIdAndUserIdAsync(string id, string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーのチャットセッション一覧を更新日時の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーの会話履歴一覧の表示に使用されます。
    /// 最近更新されたセッションが先頭に表示されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>UpdatedAt の降順でソートされる</description></item>
    ///   <item><description>Messages は含まれない（一覧表示用に軽量化）</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    ///   <item><description>該当するセッションがない場合は空リストを返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>チャットセッションのリスト。</returns>
    Task<List<ChatSession>> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内に作成されたチャットセッション一覧を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 管理者向けの分析やレポート生成に使用されます。
    /// 日次・週次・月次のセッション分析が可能です。
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
    /// <returns>チャットセッションのリスト。</returns>
    Task<List<ChatSession>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーのアクティブなセッション数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 同時セッション数の制限やユーザーの利用状況把握に使用されます。
    /// Status が "ACTIVE" のセッションをカウントします。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>Status = "ACTIVE" のセッションのみカウントする</description></item>
    ///   <item><description>該当するセッションがない場合は 0 を返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アクティブセッションの件数。</returns>
    Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 新しいチャットセッションを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 新規会話の開始時に呼び出されます。
    /// 追加されたセッションは <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>セッションを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    ///   <item><description>子エンティティ（Messages）も同時に追加される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="session">追加するチャットセッション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(ChatSession session, CancellationToken ct = default);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit of Work パターンに基づき、追跡中の全ての変更を
    /// 単一のトランザクションとしてデータベースにコミットします。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>追加・更新・削除された全てのエンティティを永続化する</description></item>
    ///   <item><description>失敗時は例外をスローし、変更はロールバックされる</description></item>
    ///   <item><description>CreatedAt/UpdatedAt は自動的に設定される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">データベース更新時にエラーが発生した場合。</exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">楽観的ロック競合が発生した場合。</exception>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内のチャットセッション数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// サービス利用統計やダッシュボード表示に使用されます。
    /// 期間ごとのセッション開始数を把握できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>from ≤ CreatedAt ≤ to の範囲でカウントする（両端を含む）</description></item>
    ///   <item><description>該当するセッションがない場合は 0 を返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>セッション件数。</returns>
    Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間・ステータスのチャットセッション数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ステータス別の統計分析に使用されます。
    /// 例えば、エスカレーション率の計算などに利用できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>期間とステータスの両方の条件を満たすセッションをカウントする</description></item>
    ///   <item><description>ステータスは完全一致で比較する</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="status">フィルタするステータス（"ACTIVE", "COMPLETED", "ESCALATED"）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当セッション件数。</returns>
    Task<int> CountByDateRangeAndStatusAsync(DateTime from, DateTime to, string status, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内のセッションステータス別件数分布を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ステータス分布のグラフ表示やKPI分析に使用されます。
    /// 各ステータスのセッション数を一度のクエリで取得できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>期間内の全ステータスについて件数を集計する</description></item>
    ///   <item><description>該当するセッションがないステータスは辞書に含まれない</description></item>
    ///   <item><description>空の期間の場合は空の辞書を返す</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ステータス文字列をキー、件数を値とする辞書。</returns>
    Task<Dictionary<string, int>> GetStatusDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザーのチャットセッションをページネーション付きで取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 大量のセッション履歴を持つユーザー向けに、
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
    /// <returns>セッションリストと総件数のタプル。</returns>
    Task<(List<ChatSession> Items, int TotalCount)> FindByUserIdPagedAsync(string userId, int page, int pageSize, CancellationToken ct = default);
}
