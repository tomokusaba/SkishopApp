namespace AuthService.DTOs.Events;

/// <summary>
/// ユーザー認証イベント DTO。
/// ユーザーがログインに成功した際に Kafka へ発行されるドメインイベント。
/// セキュリティ監査、アクティビティ追跡、不正アクセス検知に使用される。
/// </summary>
/// <param name="EventId">イベントの一意識別子（UUID 形式）。べき等性の保証に使用。</param>
/// <param name="UserId">認証に成功したユーザーの ID。</param>
/// <param name="SessionId">生成されたセッションの ID。</param>
/// <param name="IpAddress">リクエスト元の IP アドレス。プライバシー保護のためマスキングされる場合あり。</param>
/// <param name="UserAgent">クライアントの User-Agent ヘッダー値。デバイス識別に使用。</param>
/// <param name="AuthMethod">認証方式。"PASSWORD"（パスワード認証）、"OAUTH"（OAuth/OIDC）、"MFA"（多要素認証）等。</param>
/// <param name="MfaUsed">多要素認証（MFA）を使用したかどうか。</param>
/// <param name="OccurredAt">イベント発生日時（UTC）。</param>
public record UserAuthenticatedEvent(
    string EventId,
    string UserId,
    string SessionId,
    string? IpAddress,
    string? UserAgent,
    string AuthMethod,
    bool MfaUsed,
    DateTimeOffset OccurredAt);
