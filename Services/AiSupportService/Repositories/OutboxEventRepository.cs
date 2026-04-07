using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IOutboxEventRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の outbox_events テーブルに対する CRUD 操作を提供します。
/// Transactional Outbox パターンを実装し、DB トランザクションと
/// メッセージ発行の整合性を保証します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>FindPendingAsync は status と retry_count へのインデックスを前提とした設計</description></item>
///   <item><description>バッチサイズによる取得制限でメモリ消費を制御</description></item>
///   <item><description>created_at 昇順ソートでイベント順序性を維持</description></item>
///   <item><description>変更追跡を有効化し、ステータス更新を効率的に処理</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// BackgroundService での使用を想定し、リトライロジックは呼び出し側で実装してください。
/// </para>
/// <para>
/// <strong>Outbox パターンの実装詳細:</strong>
/// <list type="bullet">
///   <item><description>ビジネストランザクションと同一トランザクションでイベントを追加</description></item>
///   <item><description>BackgroundService が定期的に FindPendingAsync で未発行イベントを取得</description></item>
///   <item><description>イベント発行後、ステータスを PUBLISHED に更新し SaveChangesAsync</description></item>
///   <item><description>発行失敗時は RetryCount をインクリメントし、上限（5回）まで再試行</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しい Outbox イベントを追跡対象に追加します。
    /// ビジネストランザクションと同一トランザクションで呼び出すことで、
    /// アトミック性を保証します。
    /// </para>
    /// <para>
    /// <strong>使用例:</strong>
    /// <code>
    /// await using var transaction = await _context.Database.BeginTransactionAsync(ct);
    /// await _orderRepository.AddAsync(order, ct);
    /// await _outboxRepository.AddAsync(new OutboxEvent
    /// {
    ///     Id = Guid.NewGuid().ToString(),
    ///     EventType = "OrderCreated",
    ///     Payload = JsonSerializer.Serialize(order),
    ///     Status = "PENDING"
    /// }, ct);
    /// await _context.SaveChangesAsync(ct);
    /// await transaction.CommitAsync(ct);
    /// </code>
    /// </para>
    /// </remarks>
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// outbox_events テーブルから status = 'PENDING' かつ retry_count &lt; 5 の
    /// イベントを created_at 昇順で取得します。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT * FROM outbox_events
    /// WHERE status = 'PENDING' AND retry_count &lt; 5
    /// ORDER BY created_at ASC
    /// LIMIT @batchSize
    /// </code>
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// <list type="bullet">
    ///   <item><description>batchSize でメモリ消費を制御（推奨: 10〜100）</description></item>
    ///   <item><description>変更追跡が有効なため、ステータス更新後に SaveChangesAsync で反映可能</description></item>
    ///   <item><description>(status, retry_count, created_at) の複合インデックスがあると最適</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<List<OutboxEvent>> FindPendingAsync(int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == "PENDING" && e.RetryCount < 5)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbContext.SaveChangesAsync を呼び出し、追跡中の全ての変更を
    /// 単一のトランザクションでデータベースにコミットします。
    /// イベント発行後のステータス更新（PENDING → PUBLISHED / FAILED）に使用されます。
    /// </para>
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
