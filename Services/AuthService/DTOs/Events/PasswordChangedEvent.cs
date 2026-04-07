namespace AuthService.DTOs.Events;

/// <summary>
/// パスワード変更イベント DTO。
/// ユーザーがパスワードを変更またはリセットした際に Kafka へ発行されるドメインイベント。
/// 他のマイクロサービス（通知サービス、監査サービス等）で購読し、後続処理を行う。
/// </summary>
/// <param name="EventId">イベントの一意識別子（UUID 形式）。べき等性の保証に使用。</param>
/// <param name="UserId">パスワードを変更したユーザーの ID。</param>
/// <param name="ChangeType">変更種別。"CHANGED"（ユーザー自身による変更）または "RESET"（リセットリンク経由）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record PasswordChangedEvent(
    string EventId,
    string UserId,
    string ChangeType,
    DateTimeOffset OccurredAt);
