using AiSupportService.Models;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Infrastructure.Persistence;

/// <summary>
/// AiSupportService の EF Core DbContext。
/// </summary>
/// <remarks>
/// <para>
/// AI サポートサービスのデータアクセス層を提供する。
/// ユーザープロファイル・チャット・レコメンデーション・分析・需要予測・Outbox イベント等の
/// エンティティに対する CRUD 操作を Entity Framework Core 経由で実行する。
/// </para>
/// <para>
/// <b>エンティティ一覧:</b>
/// <list type="bullet">
///   <item><description><see cref="UserProfile"/>: AI 用ユーザープロファイル（閲覧履歴・購買履歴・プリファレンス）</description></item>
///   <item><description><see cref="ChatSession"/>: チャットセッション</description></item>
///   <item><description><see cref="ChatMessage"/>: チャットメッセージ</description></item>
///   <item><description><see cref="Recommendation"/>: レコメンデーション</description></item>
///   <item><description><see cref="SearchAnalytics"/>: 検索分析データ</description></item>
///   <item><description><see cref="DemandForecast"/>: 需要予測</description></item>
///   <item><description><see cref="ModelTraining"/>: AI モデルトレーニング履歴</description></item>
///   <item><description><see cref="OutboxEvent"/>: Outbox パターン用イベント</description></item>
/// </list>
/// </para>
/// <para>
/// <b>自動タイムスタンプ:</b> <see cref="SaveChangesAsync"/> をオーバーライドし、
/// <see cref="TimeProvider"/> を使用して <c>CreatedAt</c> / <c>UpdatedAt</c> プロパティを自動更新する。
/// これにより、テスト時の時刻制御が可能になる。
/// </para>
/// <para>
/// <b>楽観的ロック:</b> <see cref="UserProfile"/>、<see cref="ChatSession"/>、<see cref="ModelTraining"/>
/// には <c>RowVersion</c> プロパティがあり、楽観的同時実行制御が有効。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddDbContext&lt;AppDbContext&gt;(options =>
///     options.UseNpgsql(connectionString));
/// builder.Services.AddSingleton(TimeProvider.System);
/// </code>
/// </example>
/// <param name="options">DbContext 構成オプション。</param>
/// <param name="timeProvider">
/// 時刻取得用の抽象プロバイダー。テスト時にモック可能。
/// 本番環境では <see cref="TimeProvider.System"/> を使用する。
/// </param>
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    /// <summary>
    /// ユーザープロファイルのデータセット。
    /// </summary>
    /// <remarks>
    /// AI 用のユーザープロファイル。閲覧履歴・購買履歴・プリファレンスを JSON 形式で保持する。
    /// </remarks>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>
    /// チャットセッションのデータセット。
    /// </summary>
    /// <remarks>
    /// AI チャットボットとのセッション。ステータス（ACTIVE, CLOSED, ESCALATED）を持つ。
    /// </remarks>
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    /// <summary>
    /// チャットメッセージのデータセット。
    /// </summary>
    /// <remarks>
    /// セッションに属する個別のメッセージ。role（user, assistant, system）で発言者を区別する。
    /// </remarks>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <summary>
    /// レコメンデーションのデータセット。
    /// </summary>
    /// <remarks>
    /// AI が生成したユーザー向け商品レコメンデーション。有効期限付き。
    /// </remarks>
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    /// <summary>
    /// 検索分析データのデータセット。
    /// </summary>
    /// <remarks>
    /// ユーザーの検索行動を記録。クエリ・検索種別・結果件数・クリック情報を含む。
    /// </remarks>
    public DbSet<SearchAnalytics> SearchAnalytics => Set<SearchAnalytics>();

    /// <summary>
    /// 需要予測のデータセット。
    /// </summary>
    /// <remarks>
    /// AI による商品需要予測。予測期間・予測数量・信頼度スコアを含む。
    /// </remarks>
    public DbSet<DemandForecast> DemandForecasts => Set<DemandForecast>();

    /// <summary>
    /// モデルトレーニング履歴のデータセット。
    /// </summary>
    /// <remarks>
    /// AI モデルのトレーニング実行履歴。ステータス・評価メトリクスを記録する。
    /// </remarks>
    public DbSet<ModelTraining> ModelTrainings => Set<ModelTraining>();

    /// <summary>
    /// Outbox イベントのデータセット。
    /// </summary>
    /// <remarks>
    /// Outbox パターン用のイベントキュー。<see cref="OutboxPublisher"/> が Kafka に発行する。
    /// </remarks>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>
    /// エンティティの Fluent API 設定（インデックス・制約・リレーション・デフォルト値）を定義する。
    /// </summary>
    /// <param name="modelBuilder">EF Core モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// <b>設定内容:</b>
    /// <list type="bullet">
    ///   <item><description>インデックス: クエリ性能最適化用の各種インデックス</description></item>
    ///   <item><description>CHECK 制約: ステータス値・数値範囲の検証</description></item>
    ///   <item><description>外部キー: エンティティ間のリレーションと削除動作</description></item>
    ///   <item><description>デフォルト値: CreatedAt / UpdatedAt の CURRENT_TIMESTAMP</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- UserProfile ---
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique().HasDatabaseName("uq_user_profiles_user_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.LastActivityAt).HasDatabaseName("idx_user_profiles_last_activity");
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // --- ChatSession ---
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_chat_sessions_user_id");
            entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("idx_chat_sessions_user_status");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_chat_sessions_status",
                "status IN ('ACTIVE', 'CLOSED', 'ESCALATED')"));
            entity.HasOne(e => e.UserProfile)
                .WithMany(p => p.ChatSessions)
                .HasForeignKey(e => e.UserProfileId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // --- ChatMessage ---
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_chat_messages_session_id");
            entity.HasIndex(e => new { e.SessionId, e.CreatedAt }).HasDatabaseName("idx_chat_messages_session_created");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_chat_messages_role",
                "role IN ('user', 'assistant', 'system')"));
            entity.HasOne(e => e.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Recommendation ---
        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_recommendations_user_id");
            entity.HasIndex(e => new { e.UserId, e.Type }).HasDatabaseName("idx_recommendations_user_type");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_recommendations_type",
                "type IN ('PERSONALIZED', 'TRENDING', 'SIMILAR', 'SEASONAL', 'FREQUENTLY_BOUGHT_TOGETHER')"));
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("idx_recommendations_expires");
            entity.HasOne(e => e.UserProfile)
                .WithMany(p => p.Recommendations)
                .HasForeignKey(e => e.UserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- SearchAnalytics ---
        modelBuilder.Entity<SearchAnalytics>(entity =>
        {
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_search_analytics_user_id");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_search_analytics_created_at");
            entity.HasIndex(e => e.Query).HasDatabaseName("idx_search_analytics_query");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_search_analytics_search_type",
                "search_type IN ('KEYWORD', 'SEMANTIC', 'HYBRID')"));
        });

        // --- DemandForecast ---
        modelBuilder.Entity<DemandForecast>(entity =>
        {
            entity.HasIndex(e => e.ProductId).HasDatabaseName("idx_demand_forecasts_product_id");
            entity.HasIndex(e => new { e.ProductId, e.ForecastPeriod })
                .HasDatabaseName("idx_demand_forecasts_product_period");
            entity.HasIndex(e => new { e.ProductId, e.ForecastDate })
                .HasDatabaseName("idx_demand_forecasts_product_date");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_demand_forecasts_predicted_demand",
                "predicted_demand >= 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_demand_forecasts_confidence",
                "confidence_score >= 0 AND confidence_score <= 1"));
        });

        // --- ModelTraining ---
        modelBuilder.Entity<ModelTraining>(entity =>
        {
            entity.HasIndex(e => e.ModelName).HasDatabaseName("idx_model_trainings_model_name");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_model_trainings_status",
                "status IN ('PENDING', 'TRAINING', 'COMPLETED', 'FAILED')"));
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        // --- OutboxEvent ---
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_events_status_created");
            entity.HasIndex(e => new { e.AggregateType, e.AggregateId })
                .HasDatabaseName("idx_outbox_events_aggregate");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_status",
                "status IN ('PENDING', 'PUBLISHED', 'FAILED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_retry_count",
                "retry_count >= 0"));
        });
    }

    /// <summary>
    /// 変更の保存時に、追加・更新されたエンティティの <c>CreatedAt</c> / <c>UpdatedAt</c> を
    /// <see cref="TimeProvider"/> 経由の現在 UTC 時刻で自動更新する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>データベースに書き込まれた状態エントリの数。</returns>
    /// <remarks>
    /// <para>
    /// <b>処理内容:</b>
    /// <list type="bullet">
    ///   <item><description>Added 状態のエンティティ: CreatedAt と UpdatedAt の両方を設定</description></item>
    ///   <item><description>Modified 状態のエンティティ: UpdatedAt のみを更新</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>TimeProvider 使用理由:</b> <c>DateTime.UtcNow</c> を直接使用すると
    /// ユニットテストで時刻を制御できないため、DI 経由の <see cref="TimeProvider"/> を使用する。
    /// </para>
    /// <para>
    /// <b>楽観的ロック:</b> RowVersion プロパティを持つエンティティで競合が発生した場合、
    /// <see cref="DbUpdateConcurrencyException"/> がスローされる。
    /// </para>
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in entries)
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") is not null)
                entry.Property("CreatedAt").CurrentValue = now;
        }

        return await base.SaveChangesAsync(ct);
    }
}
