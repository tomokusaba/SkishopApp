using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing OutboxEvent entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> OutboxEvent
/// </para>
/// <para>
/// このリポジトリは Transactional Outbox パターンを実装するための
/// イベントメッセージを管理します。マイクロサービス間のイベント駆動通信において、
/// データベーストランザクションとメッセージ発行の整合性を保証します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>イベントメッセージの永続化</description></item>
///   <item><description>未発行イベントの取得（バッチ処理用）</description></item>
///   <item><description>イベント発行状態の管理</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>イベントステータス:</strong>
/// <list type="bullet">
///   <item><description>PENDING: 発行待ち</description></item>
///   <item><description>PUBLISHED: 発行完了</description></item>
///   <item><description>FAILED: 発行失敗（リトライ対象）</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Outbox パターンの利点:</strong>
/// <list type="bullet">
///   <item><description>DB 書き込みとイベント発行のアトミック性を保証</description></item>
///   <item><description>メッセージブローカー障害時もデータ損失を防止</description></item>
///   <item><description>リトライ機構による確実なメッセージ配信</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IOutboxEventRepository
{
    /// <summary>
    /// 新しい Outbox イベントを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ビジネストランザクションと同一トランザクション内でイベントを記録します。
    /// これにより、ビジネスデータの永続化とイベント記録のアトミック性が保証されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>イベントを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    ///   <item><description>初期 Status は "PENDING" を想定</description></item>
    ///   <item><description>RetryCount は 0 で初期化されている必要がある</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>使用例:</strong>
    /// <code>
    /// await using var transaction = await _context.Database.BeginTransactionAsync(ct);
    /// await _orderRepository.AddAsync(order, ct);
    /// await _outboxRepository.AddAsync(new OutboxEvent { ... }, ct);
    /// await _context.SaveChangesAsync(ct);
    /// await transaction.CommitAsync(ct);
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="outboxEvent">追加する Outbox イベント。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);

    /// <summary>
    /// 未発行（PENDING）かつリトライ上限未到達の Outbox イベントを取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// BackgroundService による定期的なイベント発行処理で使用されます。
    /// 作成日時順に取得することで、イベントの順序性を維持します。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>Status = "PENDING" のイベントのみを取得する</description></item>
    ///   <item><description>RetryCount &lt; 5 のイベントのみを取得する（最大リトライ回数: 5）</description></item>
    ///   <item><description>CreatedAt の昇順でソートされる（古いイベントから処理）</description></item>
    ///   <item><description>batchSize で指定された件数まで取得する</description></item>
    ///   <item><description>変更追跡が有効（ステータス更新可能な状態で取得）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// batchSize は処理能力に応じて適切な値を設定してください。
    /// 大きすぎると処理時間が長くなり、タイムアウトのリスクがあります。
    /// </para>
    /// </remarks>
    /// <param name="batchSize">一度に取得する最大件数（推奨: 10〜100）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>未発行イベントのリスト。</returns>
    Task<List<OutboxEvent>> FindPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit of Work パターンに基づき、追跡中の全ての変更を
    /// 単一のトランザクションとしてデータベースにコミットします。
    /// イベント発行後のステータス更新に使用されます。
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">データベース更新時にエラーが発生した場合。</exception>
    Task SaveChangesAsync(CancellationToken ct = default);
}
