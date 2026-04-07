using System.ComponentModel.DataAnnotations;

namespace AuthService.Configurations;

/// <summary>
/// Apache Kafka 接続設定。
/// 認証イベント（UserRegisteredEvent、UserAuthenticatedEvent 等）の発行に使用される。
/// appsettings.json の "Kafka" セクションにバインドされる。
/// IOptions&lt;T&gt; でのバインディングに対応するため init プロパティを使用。
/// </summary>
/// <remarks>
/// <para>発行されるトピック:</para>
/// <list type="bullet">
///   <item><description>auth.user.registered - ユーザー登録イベント</description></item>
///   <item><description>auth.user.authenticated - ユーザー認証イベント</description></item>
///   <item><description>auth.password.changed - パスワード変更イベント</description></item>
/// </list>
/// </remarks>
public record KafkaSettings
{
    /// <summary>Kafka ブローカーのアドレス。カンマ区切りで複数指定可能（例: "broker1:9092,broker2:9092"）。</summary>
    [Required]
    public string BootstrapServers { get; init; } = string.Empty;
}
