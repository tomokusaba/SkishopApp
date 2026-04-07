namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// Outbox パターンに基づくイベント発行サービスのインターフェース。
/// ドメインイベントを Outbox テーブルに永続化し、BackgroundService による非同期 Kafka 配信を保証する。
/// </summary>
public interface IEventPublisherService
{
    /// <summary>
    /// 汎用イベントを Outbox テーブルに登録する。DB トランザクションとイベント発行の整合性を保証する。
    /// </summary>
    /// <typeparam name="TEvent">イベントペイロードの型</typeparam>
    /// <param name="eventType">イベント種別（例: ProductCreated, InventoryReserved）</param>
    /// <param name="topic">Kafka トピック名</param>
    /// <param name="aggregateId">Aggregate の識別子</param>
    /// <param name="aggregateType">Aggregate の型名（例: Product, Inventory）</param>
    /// <param name="payload">イベントペイロード</param>
    /// <param name="ct">キャンセルトークン</param>
    Task PublishAsync<TEvent>(
        string eventType, string topic, string aggregateId, string aggregateType,
        TEvent payload, CancellationToken ct = default) where TEvent : class;

    /// <summary>
    /// 商品関連イベントを inventory.products トピックに発行する。
    /// </summary>
    /// <param name="eventType">イベント種別</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="payload">イベントペイロード</param>
    /// <param name="ct">キャンセルトークン</param>
    Task PublishProductEventAsync(string eventType, string productId, object payload, CancellationToken ct = default);

    /// <summary>
    /// 在庫関連イベントを inventory.levels トピックに発行する。
    /// </summary>
    /// <param name="eventType">イベント種別</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="payload">イベントペイロード</param>
    /// <param name="ct">キャンセルトークン</param>
    Task PublishInventoryEventAsync(string eventType, string productId, object payload, CancellationToken ct = default);

    /// <summary>
    /// 価格関連イベントを inventory.pricing トピックに発行する。
    /// </summary>
    /// <param name="eventType">イベント種別</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="payload">イベントペイロード</param>
    /// <param name="ct">キャンセルトークン</param>
    Task PublishPriceEventAsync(string eventType, string productId, object payload, CancellationToken ct = default);
}
