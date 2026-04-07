namespace AuthService.DTOs.Events;

/// <summary>
/// ユーザー登録イベント DTO。
/// 新規ユーザーが登録された際に Kafka へ発行されるドメインイベント。
/// 他のマイクロサービス（ユーザー管理サービス、メール送信サービス等）で購読し、
/// プロファイル初期化やウェルカムメール送信などの後続処理を行う。
/// </summary>
/// <param name="EventId">イベントの一意識別子（UUID 形式）。べき等性の保証に使用。</param>
/// <param name="UserId">登録されたユーザーの ID。</param>
/// <param name="Role">割り当てられたロール。"USER"（一般ユーザー）または "ADMIN"（管理者）。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record UserRegisteredEvent(
    string EventId,
    string UserId,
    string Role,
    DateTimeOffset OccurredAt);
