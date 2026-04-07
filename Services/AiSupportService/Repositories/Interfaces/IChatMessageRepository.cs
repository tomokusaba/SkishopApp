using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing ChatMessage entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> ChatSession（ChatMessage は ChatSession の子エンティティ）
/// </para>
/// <para>
/// このリポジトリはチャットメッセージの読み取り専用操作を提供します。
/// メッセージの追加・更新は親エンティティである ChatSession を通じて行われます。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>セッション単位でのメッセージ取得</description></item>
///   <item><description>メッセージ統計情報の集計（件数カウント）</description></item>
///   <item><description>期間指定でのメッセージ分析</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IChatMessageRepository
{
    /// <summary>
    /// 指定されたセッションのメッセージ一覧を作成日時の昇順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 会話履歴の表示に使用されます。時系列順に並べ替えて返却するため、
    /// 会話の流れを正しく表現できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しないセッション ID を指定した場合は空リストを返す</description></item>
    ///   <item><description>メッセージは CreatedAt の昇順でソートされる</description></item>
    ///   <item><description>読み取り専用クエリとして実行される（変更追跡なし）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="sessionId">セッション ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>チャットメッセージのリスト。該当するメッセージがない場合は空リスト。</returns>
    Task<List<ChatMessage>> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたセッションのメッセージ数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 会話の長さの把握やセッション統計に使用されます。
    /// 全件取得せずにカウントのみを行うため、パフォーマンスに優れています。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しないセッション ID を指定した場合は 0 を返す</description></item>
    ///   <item><description>ユーザーメッセージとアシスタントメッセージの両方をカウントする</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="sessionId">セッション ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メッセージ件数。</returns>
    Task<int> CountBySessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 指定された期間内のメッセージ数を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI サポートサービスの利用統計や分析レポートの生成に使用されます。
    /// 日次・週次・月次のメッセージ量を把握できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>from ≤ CreatedAt ≤ to の範囲でカウントする（両端を含む）</description></item>
    ///   <item><description>該当するメッセージがない場合は 0 を返す</description></item>
    ///   <item><description>日時は UTC として扱われる</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="from">期間の開始日時（UTC）。</param>
    /// <param name="to">期間の終了日時（UTC）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メッセージ件数。</returns>
    Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
