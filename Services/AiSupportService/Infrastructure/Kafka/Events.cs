namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// 商品更新イベント。商品の作成または更新時に Kafka から受信する。
/// </summary>
/// <remarks>
/// <para>
/// <c>product.created</c> および <c>product.updated</c> トピックで配信されるイベント。
/// <see cref="ProductIndexSyncConsumer"/> がこのイベントを受信し、AI 検索用インデックスを更新する。
/// </para>
/// <para>
/// <b>イベントソース:</b> InventoryManagementService
/// </para>
/// </remarks>
/// <param name="ProductId">商品の一意識別子。</param>
/// <param name="Name">商品名。</param>
/// <param name="Description">商品説明（オプション）。</param>
/// <param name="Price">商品価格（税込）。</param>
/// <param name="Category">商品カテゴリ（オプション）。</param>
/// <param name="ImageUrl">商品画像 URL（オプション）。</param>
/// <param name="EventType">イベント種別（"Created" または "Updated"）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record ProductUpdatedEvent(
    string ProductId, string Name, string? Description, decimal Price,
    string? Category, string? ImageUrl, string EventType, DateTime OccurredAt);

/// <summary>
/// 商品削除イベント。商品削除時に Kafka から受信する。
/// </summary>
/// <remarks>
/// <para>
/// <c>product.deleted</c> トピックで配信されるイベント。
/// <see cref="ProductIndexSyncConsumer"/> がこのイベントを受信し、AI 検索用インデックスから商品を削除する。
/// </para>
/// <para>
/// <b>イベントソース:</b> InventoryManagementService
/// </para>
/// </remarks>
/// <param name="ProductId">削除された商品の一意識別子。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record ProductDeletedEvent(string ProductId, DateTime OccurredAt);

/// <summary>
/// 注文作成イベント。注文確定時に Kafka から受信し、購買履歴の更新に使用する。
/// </summary>
/// <remarks>
/// <para>
/// <c>order.created</c> トピックで配信されるイベント。
/// <see cref="OrderCreatedConsumer"/> がこのイベントを受信し、ユーザープロファイルの購買履歴を更新する。
/// </para>
/// <para>
/// <b>イベントソース:</b> SalesManagementService
/// </para>
/// <para>
/// <b>データ活用:</b> 購買履歴はパーソナライズドレコメンデーションの生成に使用される。
/// </para>
/// </remarks>
/// <param name="OrderId">注文の一意識別子。</param>
/// <param name="UserId">注文者のユーザー ID。</param>
/// <param name="Items">注文明細のリスト。</param>
/// <param name="TotalAmount">注文合計金額。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record OrderCreatedEvent(
    string OrderId, string UserId, List<OrderItemEvent> Items,
    decimal TotalAmount, DateTime OccurredAt);

/// <summary>
/// 注文明細イベント。<see cref="OrderCreatedEvent"/> に含まれる個別の注文明細。
/// </summary>
/// <param name="ProductId">商品 ID。</param>
/// <param name="ProductName">商品名（スナップショット）。</param>
/// <param name="Quantity">数量。</param>
/// <param name="UnitPrice">単価。</param>
public record OrderItemEvent(string ProductId, string ProductName, int Quantity, decimal UnitPrice);

/// <summary>
/// ユーザー登録イベント。新規ユーザー登録時に Kafka から受信する。
/// </summary>
/// <remarks>
/// <para>
/// <c>user.registered</c> トピックで配信されるイベント。
/// <see cref="UserRegisteredConsumer"/> がこのイベントを受信し、AI サービス用のユーザープロファイルを作成する。
/// </para>
/// <para>
/// <b>イベントソース:</b> AuthService
/// </para>
/// </remarks>
/// <param name="UserId">登録されたユーザーの一意識別子。</param>
/// <param name="Email">ユーザーのメールアドレス。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record UserRegisteredEvent(string UserId, string Email, DateTime OccurredAt);

/// <summary>
/// ユーザー削除要求イベント。GDPR 等のデータ削除要求時に Kafka から受信する。
/// </summary>
/// <remarks>
/// <para>
/// <c>user.deletion.requested</c> トピックで配信されるイベント。
/// <see cref="UserDeletionConsumer"/> がこのイベントを受信し、当該ユーザーに関連する全データを削除する。
/// </para>
/// <para>
/// <b>イベントソース:</b> UserManagementService
/// </para>
/// <para>
/// <b>GDPR 対応:</b> このイベントの処理完了後、<see cref="UserDeletionCompletedEvent"/> を発行して
/// オーケストレーターに削除完了を通知する。
/// </para>
/// </remarks>
/// <param name="UserId">削除対象のユーザー ID。</param>
/// <param name="RequestId">削除要求の一意識別子（トラッキング用）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record UserDeletionRequestedEvent(string UserId, string RequestId, DateTime OccurredAt);

/// <summary>
/// レコメンデーション生成イベント。AI によるレコメンデーション生成完了時に発行する。
/// </summary>
/// <remarks>
/// <para>
/// <c>ai.recommendation.generated</c> トピックに発行されるイベント。
/// 他のサービスがレコメンデーション更新を検知するために使用できる。
/// </para>
/// <para>
/// <b>イベント発行元:</b> AiSupportService（Outbox パターン経由）
/// </para>
/// </remarks>
/// <param name="UserId">レコメンデーション対象のユーザー ID。</param>
/// <param name="Type">レコメンデーション種別（PERSONALIZED, TRENDING, SIMILAR, SEASONAL 等）。</param>
/// <param name="ProductIds">レコメンド商品 ID のリスト。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record RecommendationGeneratedEvent(
    string UserId, string Type, List<string> ProductIds, DateTime OccurredAt);

/// <summary>
/// チャットセッションエスカレーションイベント。AI 対応からオペレーターへのエスカレーション時に発行する。
/// </summary>
/// <remarks>
/// <para>
/// <c>ai.chat.escalated</c> トピックに発行されるイベント。
/// カスタマーサポートシステムが受信し、オペレーターへの引き継ぎ処理を開始する。
/// </para>
/// <para>
/// <b>エスカレーション条件:</b> ユーザーが明示的にオペレーター対応を希望した場合、
/// または AI が回答困難と判断した場合に発行される。
/// </para>
/// </remarks>
/// <param name="SessionId">エスカレーション対象のセッション ID。</param>
/// <param name="UserId">セッションのユーザー ID。</param>
/// <param name="Summary">AI による会話要約（オペレーター引き継ぎ用）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record ChatSessionEscalatedEvent(
    string SessionId, string UserId, string Summary, DateTime OccurredAt);

/// <summary>
/// 需要予測生成イベント。AI による需要予測完了時に発行する。
/// </summary>
/// <remarks>
/// <para>
/// <c>ai.forecast.generated</c> トピックに発行されるイベント。
/// 在庫管理サービスが受信し、自動発注や在庫調整の判断に使用する。
/// </para>
/// <para>
/// <b>予測精度:</b> <paramref name="ConfidenceScore"/> が予測の信頼度を示す（0.0〜1.0）。
/// 低い信頼度の予測は参考値として扱うことを推奨する。
/// </para>
/// </remarks>
/// <param name="ProductId">予測対象の商品 ID。</param>
/// <param name="ForecastPeriod">予測対象期間（例: "2026-04", "2026-Q2"）。</param>
/// <param name="PredictedDemand">予測需要数量。</param>
/// <param name="ConfidenceScore">予測信頼度スコア（0.0〜1.0）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record ForecastGeneratedEvent(
    string ProductId, string ForecastPeriod, int PredictedDemand,
    decimal ConfidenceScore, DateTime OccurredAt);

/// <summary>
/// ユーザー削除完了イベント。当サービスでのユーザーデータ削除完了時に Outbox 経由で発行する。
/// </summary>
/// <remarks>
/// <para>
/// <c>user.deletion.completed.ai-support</c> トピックに発行されるイベント。
/// GDPR 削除オーケストレーターが全サービスからの完了通知を集約し、削除完了を記録する。
/// </para>
/// <para>
/// <b>発行タイミング:</b> <see cref="UserDeletionConsumer"/> がユーザーデータの削除を完了した後、
/// 同一トランザクション内で Outbox テーブルに追加される。
/// </para>
/// </remarks>
/// <param name="UserId">削除完了したユーザー ID。</param>
/// <param name="RequestId">削除要求の一意識別子（トラッキング用）。</param>
/// <param name="ServiceName">削除を完了したサービス名（"AiSupportService"）。</param>
/// <param name="CompletedAt">削除完了日時（UTC）。</param>
public record UserDeletionCompletedEvent(
    string UserId, string RequestId, string ServiceName, DateTime CompletedAt);
