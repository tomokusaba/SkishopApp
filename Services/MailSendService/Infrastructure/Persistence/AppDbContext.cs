using Microsoft.EntityFrameworkCore;
using MailSendService.Models;

namespace MailSendService.Infrastructure.Persistence;

/// <summary>
/// MailSendService の EF Core DbContext。全エンティティのテーブル構成と制約を Fluent API で定義する。
/// </summary>
/// <remarks>
/// <para><see cref="TimeProvider"/> を DI で受け取り、<see cref="SaveChangesAsync"/> オーバーライドで
/// エンティティの <c>CreatedAt</c> / <c>UpdatedAt</c> タイムスタンプを自動管理する。</para>
/// <para>PostgreSQL の <c>xmin</c> システムカラムを楽観的同時実行トークンとして使用する。</para>
/// </remarks>
/// <param name="options">DbContext オプション。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    /// <summary>メール送信ログテーブル。</summary>
    public DbSet<MailLog> MailLogs => Set<MailLog>();

    /// <summary>メール添付ファイルテーブル。</summary>
    public DbSet<MailAttachment> MailAttachments => Set<MailAttachment>();

    /// <summary>メールテンプレートテーブル。</summary>
    public DbSet<MailTemplate> MailTemplates => Set<MailTemplate>();

    /// <summary>メール送信抑制リストテーブル。</summary>
    public DbSet<MailSuppression> MailSuppressions => Set<MailSuppression>();

    /// <summary>Outbox イベントテーブル。</summary>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>
    /// Fluent API による全エンティティのテーブル構成・制約・インデックスを定義する。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>各エンティティに対して以下を設定する:</para>
    /// <list type="bullet">
    /// <item>テーブル名（snake_case 複数形）</item>
    /// <item>CHECK 制約（ステータス値、リトライ回数等）</item>
    /// <item>インデックス（検索・一意制約用）</item>
    /// <item>PostgreSQL <c>xmin</c> 楽観的同時実行トークン</item>
    /// <item>リレーション（MailLog → MailAttachment の CASCADE 削除）</item>
    /// </list>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MailLog>(entity =>
        {
            entity.ToTable("mail_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue(MailLogStatus.Pending);
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_logs_status",
                "status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'BOUNCED', 'SKIPPED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_logs_retry_count", "retry_count >= 0"));

            entity.HasIndex(e => e.EventId).IsUnique()
                .HasDatabaseName("idx_mail_logs_event_id");
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_mail_logs_status_created_at");
            entity.HasIndex(e => e.RecipientEmail)
                .HasDatabaseName("idx_mail_logs_recipient");
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_mail_logs_created_at");
            entity.HasIndex(e => e.TemplateId)
                .HasDatabaseName("idx_mail_logs_template_id");
            entity.HasIndex(e => e.RecipientUserId)
                .HasDatabaseName("idx_mail_logs_recipient_user_id");

            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.MailLog)
                .HasForeignKey(a => a.MailLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<MailTemplate>()
                .WithMany()
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        modelBuilder.Entity<MailAttachment>(entity =>
        {
            entity.ToTable("mail_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.MailLogId)
                .HasDatabaseName("idx_mail_attachments_mail_log_id");
        });

        modelBuilder.Entity<MailTemplate>(entity =>
        {
            entity.ToTable("mail_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Variables).HasColumnType("jsonb");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_templates_type",
                "template_type IN ('TRANSACTIONAL', 'MARKETING')"));

            entity.HasIndex(e => e.Name).IsUnique()
                .HasDatabaseName("idx_mail_templates_name");
            entity.HasIndex(e => new { e.TemplateType, e.IsActive })
                .HasDatabaseName("idx_mail_templates_type_active");

            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<MailSuppression>(entity =>
        {
            entity.ToTable("mail_suppressions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_suppressions_reason",
                "reason IN ('UNSUBSCRIBE', 'BOUNCE', 'COMPLAINT')"));

            entity.HasIndex(e => new { e.Email, e.Reason }).IsUnique()
                .HasDatabaseName("uq_mail_suppressions_email_reason");
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.ToTable("outbox_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(OutboxEventStatus.Pending);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_status",
                "status IN ('PENDING', 'PUBLISHED', 'FAILED')"));

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_events_status_created_at");
        });
    }

    /// <summary>
    /// 変更追跡中のエンティティに対して <c>CreatedAt</c> / <c>UpdatedAt</c> タイムスタンプを自動設定し、
    /// データベースに保存する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>保存された変更の件数。</returns>
    /// <remarks>
    /// 新規追加（Added）時は <c>CreatedAt</c> と <c>UpdatedAt</c> の両方を設定し、
    /// 更新（Modified）時は <c>UpdatedAt</c> のみを更新する。
    /// 時刻は <see cref="TimeProvider"/> から取得する。
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is MailLog mailLog)
            {
                if (entry.State == EntityState.Added) mailLog.CreatedAt = now;
                mailLog.UpdatedAt = now;
            }
            else if (entry.Entity is MailAttachment attachment)
            {
                if (entry.State == EntityState.Added) attachment.CreatedAt = now;
                attachment.UpdatedAt = now;
            }
            else if (entry.Entity is MailTemplate template)
            {
                if (entry.State == EntityState.Added) template.CreatedAt = now;
                template.UpdatedAt = now;
            }
            else if (entry.Entity is MailSuppression suppression)
            {
                if (entry.State == EntityState.Added) suppression.CreatedAt = now;
                suppression.UpdatedAt = now;
            }
            else if (entry.Entity is OutboxEvent outboxEvent)
            {
                if (entry.State == EntityState.Added) outboxEvent.CreatedAt = now;
                outboxEvent.UpdatedAt = now;  // P1-13: UpdatedAt を常に更新（リトライバックオフ用）
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
