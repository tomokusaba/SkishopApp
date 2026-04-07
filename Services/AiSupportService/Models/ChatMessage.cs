using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// AI チャットの個別メッセージを表すエンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは <see cref="ChatSession"/> に属する個々のメッセージを管理します。
/// ユーザーからの質問（role="user"）や AI からの応答（role="assistant"）など、
/// 会話の各ターンを時系列順に記録します。
/// </para>
/// <para>
/// テーブル名: <c>chat_messages</c>
/// </para>
/// <para>
/// 外部キー: <see cref="SessionId"/> → <see cref="ChatSession.Id"/>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var message = new ChatMessage
/// {
///     SessionId = session.Id,
///     Role = "user",
///     Content = "スキー板のおすすめを教えてください",
///     TokenCount = 25
/// };
/// </code>
/// </example>
[Table("chat_messages")]
public class ChatMessage
{
    /// <summary>
    /// メッセージの一意識別子（UUID 形式）。
    /// </summary>
    /// <remarks>
    /// 新規作成時に <see cref="Guid.NewGuid()"/> で自動生成されます。
    /// </remarks>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このメッセージが属するチャットセッションの ID。
    /// </summary>
    /// <remarks>
    /// <see cref="ChatSession.Id"/> への外部キー。必須項目です。
    /// </remarks>
    [Column("session_id")]
    [Required]
    [MaxLength(36)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// メッセージの送信者ロール。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"user"</c> - ユーザーからの入力メッセージ</description></item>
    ///   <item><description><c>"assistant"</c> - AI からの応答メッセージ</description></item>
    ///   <item><description><c>"system"</c> - システムプロンプトやコンテキスト情報</description></item>
    /// </list>
    /// </remarks>
    [Column("role")]
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// メッセージの本文。
    /// </summary>
    /// <remarks>
    /// ユーザーの質問や AI の回答など、実際のテキストコンテンツを格納します。
    /// 長文も格納可能です。
    /// </remarks>
    [Column("content")]
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// メッセージに関連するメタデータ（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 以下のような追加情報を格納するために使用します：
    /// </para>
    /// <list type="bullet">
    ///   <item><description>AI モデルの応答に関する詳細（モデル名、temperature 等）</description></item>
    ///   <item><description>参照した商品情報や検索結果へのリンク</description></item>
    ///   <item><description>ユーザーの入力デバイス情報</description></item>
    /// </list>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("metadata_json", TypeName = "jsonb")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// メッセージのトークン数。
    /// </summary>
    /// <remarks>
    /// AI モデルへの入出力時に消費されたトークン数を記録します。
    /// コスト計算やコンテキストウィンドウの管理に使用します。
    /// </remarks>
    [Column("token_count")]
    public int? TokenCount { get; set; }

    /// <summary>
    /// メッセージの作成日時（UTC）。
    /// </summary>
    /// <remarks>
    /// 会話の時系列順を維持するために使用します。
    /// 新規作成時に <see cref="DateTime.UtcNow"/> が自動設定されます。
    /// </remarks>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// このメッセージが属するチャットセッションへのナビゲーションプロパティ。
    /// </summary>
    /// <remarks>
    /// EF Core の遅延読み込みは無効です。必要に応じて <c>Include()</c> で明示的に読み込んでください。
    /// </remarks>
    public ChatSession? Session { get; set; }
}
