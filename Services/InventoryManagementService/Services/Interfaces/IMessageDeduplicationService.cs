namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// Kafka コンシューマーのべき等性を保証するメッセージ重複排除サービスのインターフェース。
/// ProcessedMessage テーブルで処理済みメッセージを追跡し、重複処理を防止する。
/// </summary>
public interface IMessageDeduplicationService
{
    /// <summary>
    /// 指定されたメッセージ ID が処理済みかどうかを確認する。
    /// </summary>
    /// <param name="messageId">Kafka メッセージ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>処理済みの場合は true</returns>
    Task<bool> IsProcessedAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// メッセージを処理済みとして記録する。トピック・パーティション・オフセット情報も保存する。
    /// </summary>
    /// <param name="messageId">Kafka メッセージ ID</param>
    /// <param name="topic">Kafka トピック名</param>
    /// <param name="partition">パーティション番号</param>
    /// <param name="offset">オフセット値</param>
    /// <param name="ct">キャンセルトークン</param>
    Task MarkAsProcessedAsync(string messageId, string topic, int partition, long offset, CancellationToken ct = default);
}
