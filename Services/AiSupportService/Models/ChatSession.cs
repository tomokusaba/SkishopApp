using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AiSupportService.Exceptions;

namespace AiSupportService.Models;

/// <summary>
/// AI チャットセッションを表すエンティティ（Aggregate Root）。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティはユーザーと AI アシスタント間の会話セッション全体を管理します。
/// セッションには複数の <see cref="ChatMessage"/> が含まれ、会話の文脈を維持します。
/// </para>
/// <para>
/// テーブル名: <c>chat_sessions</c>
/// </para>
/// <para>
/// ライフサイクル:
/// <list type="number">
///   <item><description>作成時: <see cref="ChatSessionStatus.Active"/> 状態で開始</description></item>
///   <item><description>会話終了時: <see cref="Close"/> メソッドで <see cref="ChatSessionStatus.Closed"/> に遷移</description></item>
///   <item><description>オペレーター対応が必要な場合: <see cref="Escalate"/> メソッドで <see cref="ChatSessionStatus.Escalated"/> に遷移</description></item>
/// </list>
/// </para>
/// <para>
/// 楽観的同時実行制御: <see cref="RowVersion"/> で競合を検出します。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var session = new ChatSession { UserId = "user-123", Title = "商品について" };
/// session.AddMessage("user", "スキー板のおすすめを教えてください");
/// session.AddMessage("assistant", "お客様のスキーレベルを教えていただけますか？");
/// 
/// // セッション終了時
/// session.Close();
/// </code>
/// </example>
[Table("chat_sessions")]
public class ChatSession
{
    /// <summary>
    /// セッションの一意識別子（UUID 形式）。
    /// </summary>
    /// <remarks>
    /// 新規作成時に <see cref="Guid.NewGuid()"/> で自動生成されます。
    /// </remarks>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// セッションを開始したユーザーの ID。
    /// </summary>
    /// <remarks>
    /// 認証システム（AuthService）で管理されるユーザー ID を参照します。
    /// 必須項目です。
    /// </remarks>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// セッションのタイトル。
    /// </summary>
    /// <remarks>
    /// ユーザーが設定するか、最初のメッセージから自動生成されます。
    /// 会話履歴の一覧表示時に使用します。
    /// </remarks>
    [Column("title")]
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// セッションの現在のステータス（文字列形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 有効な値: <c>"ACTIVE"</c>, <c>"CLOSED"</c>, <c>"ESCALATED"</c>
    /// </para>
    /// <para>
    /// 列挙型での取得は <see cref="StatusEnum"/> プロパティを使用してください。
    /// </para>
    /// </remarks>
    /// <seealso cref="ChatSessionStatus"/>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// セッションのコンテキスト情報（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 会話の文脈を維持するための情報を格納します：
    /// </para>
    /// <list type="bullet">
    ///   <item><description>ユーザーの意図や目的</description></item>
    ///   <item><description>参照中の商品情報</description></item>
    ///   <item><description>会話の要約</description></item>
    /// </list>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("context_json", TypeName = "jsonb")]
    public string? ContextJson { get; set; }

    /// <summary>
    /// セッションの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// セッションの最終更新日時（UTC）。
    /// </summary>
    /// <remarks>
    /// メッセージ追加、ステータス変更時に自動更新されます。
    /// </remarks>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// セッションがクローズされた日時（UTC）。
    /// </summary>
    /// <remarks>
    /// <see cref="Close"/> メソッド呼び出し時に設定されます。
    /// アクティブなセッションでは <c>null</c> です。
    /// </remarks>
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// 関連する <see cref="UserProfile"/> の ID。
    /// </summary>
    /// <remarks>
    /// AI 機能向けのユーザープロファイルへの外部キー。
    /// パーソナライズされた応答生成に使用します。
    /// </remarks>
    [ForeignKey("UserId")]
    [Column("user_profile_id")]
    [MaxLength(36)]
    public string? UserProfileId { get; set; }

    /// <summary>
    /// 関連するユーザープロファイルへのナビゲーションプロパティ。
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// このセッションに属するメッセージのコレクション。
    /// </summary>
    /// <remarks>
    /// 時系列順に <see cref="ChatMessage"/> を保持します。
    /// メッセージの追加は <see cref="AddMessage"/> メソッドを使用してください。
    /// </remarks>
    public ICollection<ChatMessage> Messages { get; set; } = [];

    // --- Domain Methods ---

    /// <summary>
    /// 文字列ステータスを <see cref="ChatSessionStatus"/> 列挙型に変換して返す。
    /// </summary>
    [NotMapped]
    public ChatSessionStatus StatusEnum => Enum.Parse<ChatSessionStatus>(Status, ignoreCase: true);

    /// <summary>
    /// セッションがアクティブ状態かどうかを判定する。
    /// </summary>
    [NotMapped]
    public bool IsActive => StatusEnum == ChatSessionStatus.Active;

    /// <summary>
    /// セッションにメッセージを追加する。
    /// </summary>
    /// <param name="role">メッセージの送信者ロール（user / assistant 等）。</param>
    /// <param name="content">メッセージ本文。</param>
    /// <returns>追加された <see cref="ChatMessage"/>。</returns>
    public ChatMessage AddMessage(string role, string content)
    {
        if (!IsActive)
            throw new BusinessException("クローズ済みセッションにメッセージを追加できません");

        var message = new ChatMessage
        {
            SessionId = Id,
            Role = role,
            Content = content
        };
        Messages.Add(message);
        UpdatedAt = DateTime.UtcNow;
        return message;
    }

    /// <summary>
    /// セッションを閉じて非アクティブにする。
    /// </summary>
    public void Close()
    {
        if (!IsActive)
            throw new BusinessException("アクティブでないセッションはクローズできません");

        Status = ChatSessionStatus.Closed.ToString().ToUpperInvariant();
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// セッションをオペレーターへエスカレーションする。
    /// </summary>
    public void Escalate()
    {
        if (!IsActive)
            throw new BusinessException("アクティブでないセッションはエスカレーションできません");

        Status = ChatSessionStatus.Escalated.ToString().ToUpperInvariant();
        UpdatedAt = DateTime.UtcNow;
    }

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];
}
