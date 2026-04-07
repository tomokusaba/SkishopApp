namespace AuthService.Enums;

/// <summary>
/// Outbox イベントの処理状態を表す列挙型。
/// Outbox パターンによるイベント発行の信頼性を保証するために使用される。
/// </summary>
/// <remarks>
/// <para>
/// 状態遷移: Pending → Processing → Published または Failed → DeadLetter
/// </para>
/// <para>
/// リトライ上限に達した場合、Failed から DeadLetter に遷移し、手動対応が必要となる。
/// </para>
/// </remarks>
public enum OutboxStatus
{
    /// <summary>
    /// 処理待ち状態。イベントが作成されたが、まだ Kafka に発行されていない。
    /// </summary>
    Pending,

    /// <summary>
    /// 処理中状態。BackgroundService がイベントを取得し、Kafka への発行を試みている。
    /// </summary>
    Processing,

    /// <summary>
    /// 発行完了状態。イベントが正常に Kafka へ発行された。
    /// </summary>
    Published,

    /// <summary>
    /// 発行失敗状態。リトライ可能な一時的エラーが発生した。次回のポーリングでリトライされる。
    /// </summary>
    Failed,

    /// <summary>
    /// デッドレター状態。リトライ上限に達し、自動リカバリ不能。運用者による手動対応が必要。
    /// </summary>
    DeadLetter
}
