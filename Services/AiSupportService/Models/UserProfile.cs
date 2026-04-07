using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// AI 機能向けのユーザープロファイルを表すエンティティ（Aggregate Root）。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは AI サービスがパーソナライズされた体験を提供するために必要な
/// ユーザー情報を集約します。認証システム（AuthService）のユーザー情報とは別に管理され、
/// AI 機能に特化したデータを格納します。
/// </para>
/// <para>
/// テーブル名: <c>user_profiles</c>
/// </para>
/// <para>
/// 主な用途:
/// <list type="bullet">
///   <item><description>パーソナライズされたレコメンデーションの生成</description></item>
///   <item><description>チャットアシスタントのコンテキスト構築</description></item>
///   <item><description>ユーザーの好みに基づく検索結果の調整</description></item>
/// </list>
/// </para>
/// <para>
/// 楽観的同時実行制御: <see cref="RowVersion"/> で競合を検出します。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var profile = new UserProfile { UserId = "user-123" };
/// 
/// // 閲覧履歴の追加
/// profile.AddBrowsingHistory(new BrowsingHistoryEntry(
///     ProductId: "prod-001",
///     ProductName: "アルペンスキー Pro",
///     ViewedAt: DateTime.UtcNow
/// ));
/// 
/// // 購買履歴の追加
/// profile.AddPurchaseHistory(new PurchaseHistoryEntry(
///     OrderId: "order-001",
///     ProductId: "prod-001",
///     ProductName: "アルペンスキー Pro",
///     Quantity: 1,
///     UnitPrice: 89800m,
///     PurchasedAt: DateTime.UtcNow
/// ));
/// </code>
/// </example>
[Table("user_profiles")]
public class UserProfile
{
    /// <summary>
    /// プロファイルの一意識別子（UUID 形式）。
    /// </summary>
    /// <remarks>
    /// AI サービス内でのユーザープロファイルの識別に使用します。
    /// <see cref="UserId"/> とは別の値です。
    /// </remarks>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 認証システムで管理されるユーザー ID。
    /// </summary>
    /// <remarks>
    /// AuthService のユーザー ID を参照します。
    /// 他のマイクロサービスとの連携時にこの ID を使用します。
    /// </remarks>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// ユーザーの好み・設定情報（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーが明示的に設定した好みや、行動から推測された好みを格納します。
    /// </para>
    /// <para>
    /// 例:
    /// <code>
    /// {
    ///   "preferredCategories": ["スキー板", "ブーツ"],
    ///   "skillLevel": "intermediate",
    ///   "priceRange": { "min": 30000, "max": 150000 },
    ///   "notificationEnabled": true
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("preferences_json", TypeName = "jsonb")]
    public string? PreferencesJson { get; set; }

    /// <summary>
    /// ユーザーの閲覧履歴（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BrowsingHistoryEntry"/> のリストを JSON 配列として格納します。
    /// <see cref="AddBrowsingHistory"/> メソッドで追加してください。
    /// </para>
    /// <para>
    /// 最大保持件数: 100 件（古いエントリは自動的に削除されます）
    /// </para>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("browsing_history_json", TypeName = "jsonb")]
    public string? BrowsingHistoryJson { get; set; }

    /// <summary>
    /// ユーザーの購買履歴（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PurchaseHistoryEntry"/> のリストを JSON 配列として格納します。
    /// <see cref="AddPurchaseHistory"/> メソッドで追加してください。
    /// </para>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("purchase_history_json", TypeName = "jsonb")]
    public string? PurchaseHistoryJson { get; set; }

    /// <summary>
    /// ユーザーの最終アクティビティ日時（UTC）。
    /// </summary>
    /// <remarks>
    /// 閲覧、購買、設定更新など、いずれかのアクションが行われた際に更新されます。
    /// アクティブユーザーの判定やリテンション分析に使用します。
    /// </remarks>
    [Column("last_activity_at")]
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// プロファイルの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// プロファイルの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// このユーザーのチャットセッションのコレクション。
    /// </summary>
    /// <remarks>
    /// EF Core の遅延読み込みは無効です。必要に応じて <c>Include()</c> で明示的に読み込んでください。
    /// </remarks>
    public ICollection<ChatSession> ChatSessions { get; set; } = [];

    /// <summary>
    /// このユーザーへのレコメンデーションのコレクション。
    /// </summary>
    /// <remarks>
    /// EF Core の遅延読み込みは無効です。必要に応じて <c>Include()</c> で明示的に読み込んでください。
    /// </remarks>
    public ICollection<Recommendation> Recommendations { get; set; } = [];

    /// <summary>
    /// 楽観的同時実行制御用のタイムスタンプ。
    /// </summary>
    /// <remarks>
    /// EF Core がレコードの競合を検出するために使用します。
    /// 更新時に自動的にインクリメントされます。
    /// </remarks>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// ユーザーの設定を更新する。
    /// </summary>
    /// <param name="preferencesJson">設定情報の JSON 文字列。</param>
    public void UpdatePreferences(string preferencesJson)
    {
        PreferencesJson = preferencesJson;
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 閲覧履歴にエントリを追加する。
    /// </summary>
    /// <param name="entry">追加する閲覧履歴エントリ。</param>
    /// <param name="maxEntries">保持する履歴の最大件数。</param>
    public void AddBrowsingHistory(BrowsingHistoryEntry entry, int maxEntries = 100)
    {
        var history = string.IsNullOrEmpty(BrowsingHistoryJson)
            ? new List<BrowsingHistoryEntry>()
            : System.Text.Json.JsonSerializer.Deserialize<List<BrowsingHistoryEntry>>(BrowsingHistoryJson) ?? [];

        history.Add(entry);
        if (history.Count > maxEntries)
            history = history.TakeLast(maxEntries).ToList();

        BrowsingHistoryJson = System.Text.Json.JsonSerializer.Serialize(history);
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 購買履歴にエントリを追加する。
    /// </summary>
    /// <param name="entry">追加する購買履歴エントリ。</param>
    public void AddPurchaseHistory(PurchaseHistoryEntry entry)
    {
        var history = string.IsNullOrEmpty(PurchaseHistoryJson)
            ? new List<PurchaseHistoryEntry>()
            : System.Text.Json.JsonSerializer.Deserialize<List<PurchaseHistoryEntry>>(PurchaseHistoryJson) ?? [];

        history.Add(entry);
        PurchaseHistoryJson = System.Text.Json.JsonSerializer.Serialize(history);
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 商品閲覧履歴の 1 エントリを表す Value Object。
/// </summary>
/// <remarks>
/// <para>
/// ユーザーが商品詳細ページを閲覧した際に記録されるエントリです。
/// <see cref="UserProfile.AddBrowsingHistory"/> メソッドで追加され、
/// <see cref="UserProfile.BrowsingHistoryJson"/> に JSON 配列として格納されます。
/// </para>
/// <para>
/// レコメンデーションエンジンが類似商品や関連商品を提案する際の入力データとして使用されます。
/// </para>
/// </remarks>
/// <param name="ProductId">閲覧した商品の ID。</param>
/// <param name="ProductName">閲覧した商品の名前（履歴表示用）。</param>
/// <param name="ViewedAt">閲覧日時（UTC）。</param>
/// <example>
/// <code>
/// var entry = new BrowsingHistoryEntry(
///     ProductId: "prod-001",
///     ProductName: "アルペンスキー Pro 2024",
///     ViewedAt: DateTime.UtcNow
/// );
/// profile.AddBrowsingHistory(entry);
/// </code>
/// </example>
public record BrowsingHistoryEntry(string ProductId, string ProductName, DateTime ViewedAt);

/// <summary>
/// 購買履歴の 1 エントリを表す Value Object。
/// </summary>
/// <remarks>
/// <para>
/// ユーザーが商品を購入した際に記録されるエントリです。
/// <see cref="UserProfile.AddPurchaseHistory"/> メソッドで追加され、
/// <see cref="UserProfile.PurchaseHistoryJson"/> に JSON 配列として格納されます。
/// </para>
/// <para>
/// パーソナライズされたレコメンデーションや、リピート購入の予測に使用されます。
/// </para>
/// </remarks>
/// <param name="OrderId">注文 ID。SalesManagementService で管理される注文を参照。</param>
/// <param name="ProductId">購入した商品の ID。</param>
/// <param name="ProductName">購入した商品の名前（履歴表示用）。</param>
/// <param name="Quantity">購入数量。</param>
/// <param name="UnitPrice">購入時の単価（税込）。</param>
/// <param name="PurchasedAt">購入日時（UTC）。</param>
/// <example>
/// <code>
/// var entry = new PurchaseHistoryEntry(
///     OrderId: "order-001",
///     ProductId: "prod-001",
///     ProductName: "アルペンスキー Pro 2024",
///     Quantity: 1,
///     UnitPrice: 89800m,
///     PurchasedAt: DateTime.UtcNow
/// );
/// profile.AddPurchaseHistory(entry);
/// </code>
/// </example>
public record PurchaseHistoryEntry(string OrderId, string ProductId, string ProductName, int Quantity, decimal UnitPrice, DateTime PurchasedAt);
