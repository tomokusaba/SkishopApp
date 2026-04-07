using MailSendService.Infrastructure.Persistence;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MailSendService.Repositories;

/// <summary>
/// <see cref="OutboxEvent"/> エンティティの EF Core リポジトリ実装。
/// </summary>
/// <remarks>
/// Outbox パターン用のイベント永続化と取得を行う。
/// PENDING / FAILED ステータスのイベントを <see cref="OutboxPublisher"/> が使用する。
/// </remarks>
/// <param name="context">アプリケーション DbContext。</param>
public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    /// <summary>
    /// 未発行（PENDING）の Outbox イベントを作成日昇順で取得する。
    /// </summary>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>PENDING ステータスのイベントリスト。</returns>
    public async Task<List<OutboxEvent>> FindPendingAsync(int limit, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// 発行失敗（FAILED）の Outbox イベントを作成日昇順で取得する。
    /// </summary>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>FAILED ステータスのイベントリスト。</returns>
    public async Task<List<OutboxEvent>> FindFailedAsync(int limit, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Failed)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// 新しい Outbox イベントを追加する。
    /// </summary>
    /// <param name="outboxEvent">追加する Outbox イベントエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    /// <summary>
    /// データベーストランザクションを開始する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>開始されたトランザクション。</returns>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => await context.Database.BeginTransactionAsync(ct);

    /// <summary>
    /// トランザクションをコミットする。
    /// </summary>
    /// <param name="transaction">コミット対象のトランザクション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken ct = default)
        => await transaction.CommitAsync(ct);

    /// <summary>
    /// トランザクションをロールバックする。
    /// </summary>
    /// <param name="transaction">ロールバック対象のトランザクション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task RollbackTransactionAsync(IDbContextTransaction transaction, CancellationToken ct = default)
        => await transaction.RollbackAsync(ct);

    /// <inheritdoc />
    public async Task<int> DeletePublishedOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Published && e.PublishedAt < cutoff)
            .ExecuteDeleteAsync(ct);
}
