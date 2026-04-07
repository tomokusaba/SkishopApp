using InventoryManagementService.Models;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// OutboxEvent エンティティのリポジトリインターフェース。
/// Outbox パターンでのイベント永続化を担当する。
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// OutboxEvent を追加する。
    /// </summary>
    /// <param name="outboxEvent">追加する Outbox イベント</param>
    /// <param name="ct">キャンセルトークン</param>
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);

    /// <summary>
    /// 変更を永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    Task SaveChangesAsync(CancellationToken ct = default);
}
