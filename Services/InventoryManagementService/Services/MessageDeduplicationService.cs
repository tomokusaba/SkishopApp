using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.Services;

/// <summary>
/// Kafka コンシューマーのべき等性を保証するメッセージ重複排除サービスの実装クラス。
/// ProcessedMessage テーブルにメッセージ ID を記録し、重複処理を防止する。
/// </summary>
/// <remarks>
/// Kafka コンシューマーの at-least-once 配信保証に対応するため、
/// メッセージ処理前に IsProcessedAsync で重複チェックを行い、処理完了後に MarkAsProcessedAsync で記録する。
/// H-6: AppDbContext 直接参照を IProcessedMessageRepository 経由に変更。
/// </remarks>
public class MessageDeduplicationService(
    IProcessedMessageRepository repository,
    ILogger<MessageDeduplicationService> logger) : IMessageDeduplicationService
{
    /// <inheritdoc />
    public async Task<bool> IsProcessedAsync(string messageId, CancellationToken ct = default)
        => await repository.ExistsAsync(messageId, ct);

    /// <inheritdoc />
    public async Task MarkAsProcessedAsync(
        string messageId, string topic, int partition, long offset, CancellationToken ct = default)
    {
        await repository.AddAsync(new ProcessedMessage
        {
            MessageId = messageId,
            Topic = topic,
            Partition = partition,
            Offset = offset
        }, ct);
        await repository.SaveChangesAsync(ct);
        logger.LogDebug("メッセージ処理済み記録: MessageId={MessageId}, Topic={Topic}", messageId, topic);
    }
}
