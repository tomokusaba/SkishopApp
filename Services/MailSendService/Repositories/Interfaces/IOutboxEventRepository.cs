using MailSendService.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace MailSendService.Repositories.Interfaces;

/// <summary>
/// Outbox パターンのイベント永続化および検索を提供するリポジトリインターフェース。
/// </summary>
public interface IOutboxEventRepository
{
    /// <summary>
    /// 発行待ち（PENDING）の Outbox イベントを取得する。
    /// </summary>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>発行待ちイベントの一覧。</returns>
    Task<List<OutboxEvent>> FindPendingAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// 発行失敗（FAILED）の Outbox イベントを取得する。
    /// </summary>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>発行失敗イベントの一覧。</returns>
    Task<List<OutboxEvent>> FindFailedAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// 新しい Outbox イベントを追加する。
    /// </summary>
    /// <param name="outboxEvent">追加する Outbox イベントエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// データベーストランザクションを開始する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>開始されたトランザクション。</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// トランザクションをコミットする。
    /// </summary>
    /// <param name="transaction">コミット対象のトランザクション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// トランザクションをロールバックする。
    /// </summary>
    /// <param name="transaction">ロールバック対象のトランザクション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task RollbackTransactionAsync(IDbContextTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// 指定日時より前に発行済み（PUBLISHED）となった Outbox イベントを物理削除する。
    /// </summary>
    /// <param name="cutoff">この日時より前のイベントを削除対象とする。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>削除されたレコード数。</returns>
    Task<int> DeletePublishedOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
