using AuthService.Enums;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// 認証サービスのデータベースコンテキスト。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、Entity Framework Core を使用して PostgreSQL データベースとの
/// 通信を管理します。認証に関連するすべてのエンティティのマッピングと
/// クエリ操作を提供します。
/// </para>
/// <para>
/// <strong>管理されるエンティティ:</strong>
/// <list type="bullet">
///   <item><see cref="User"/> - ユーザーアカウント</item>
///   <item><see cref="UserSession"/> - ユーザーセッション</item>
///   <item><see cref="Role"/> - ロール定義</item>
///   <item><see cref="UserRole"/> - ユーザーとロールの関連付け</item>
///   <item><see cref="OAuthAccount"/> - OAuth 連携アカウント</item>
///   <item><see cref="PasswordReset"/> - パスワードリセットトークン</item>
///   <item><see cref="UserMfa"/> - MFA 設定</item>
///   <item><see cref="SecurityLog"/> - セキュリティイベントログ</item>
///   <item><see cref="RefreshToken"/> - リフレッシュトークン</item>
///   <item><see cref="OutboxEvent"/> - Outbox イベント</item>
///   <item><see cref="PasswordHistory"/> - パスワード履歴</item>
///   <item><see cref="OAuthClient"/> - OAuth クライアント</item>
///   <item><see cref="AuditLog"/> - 監査ログ</item>
/// </list>
/// </para>
/// <para>
/// <strong>タイムスタンプの自動設定:</strong>
/// <see cref="SaveChangesAsync"/> をオーバーライドし、<see cref="IHasTimestamps"/> を実装する
/// エンティティの <c>CreatedAt</c> と <c>UpdatedAt</c> を自動的に設定します。
/// </para>
/// <para>
/// <strong>enum の UPPER_SNAKE_CASE 変換:</strong>
/// <see cref="UpperSnakeCaseEnumConverter{TEnum}"/> を使用して、C# の enum 値を
/// データベースの UPPER_SNAKE_CASE 文字列に変換します。
/// </para>
/// </remarks>
/// <param name="options">データベースコンテキストのオプション。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
public class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    /// <summary>
    /// ユーザーエンティティのデータセット。
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// ユーザーセッションエンティティのデータセット。
    /// </summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>
    /// ロールエンティティのデータセット。
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// ユーザーロール関連付けエンティティのデータセット。
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    /// OAuth アカウントエンティティのデータセット。
    /// </summary>
    public DbSet<OAuthAccount> OAuthAccounts => Set<OAuthAccount>();

    /// <summary>
    /// パスワードリセットエンティティのデータセット。
    /// </summary>
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    /// <summary>
    /// MFA 設定エンティティのデータセット。
    /// </summary>
    public DbSet<UserMfa> UserMfa => Set<UserMfa>();

    /// <summary>
    /// セキュリティログエンティティのデータセット。
    /// </summary>
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();

    /// <summary>
    /// リフレッシュトークンエンティティのデータセット。
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Outbox イベントエンティティのデータセット。
    /// </summary>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>
    /// パスワード履歴エンティティのデータセット。
    /// </summary>
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    /// <summary>
    /// OAuth クライアントエンティティのデータセット。
    /// </summary>
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    /// <summary>
    /// 監査ログエンティティのデータセット。
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// モデルの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// このメソッドでは、各エンティティのインデックス、制約、リレーションシップ、
    /// 初期データを構成します。
    /// </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureUserSession(modelBuilder);
        ConfigureOAuthAccount(modelBuilder);
        ConfigurePasswordReset(modelBuilder);
        ConfigureUserMfa(modelBuilder);
        ConfigureSecurityLog(modelBuilder);
        ConfigureRefreshToken(modelBuilder);
        ConfigureOutboxEvent(modelBuilder);
        ConfigurePasswordHistory(modelBuilder);
        ConfigureOAuthClient(modelBuilder);
        ConfigureAuditLog(modelBuilder);
    }

    /// <summary>
    /// User エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// <strong>設定内容:</strong>
    /// <list type="bullet">
    ///   <item>Email の一意制約</item>
    ///   <item>Username の一意制約（NULL を除く）</item>
    ///   <item>Status と Role の enum 変換（UPPER_SNAKE_CASE）</item>
    ///   <item>CHECK 制約による有効値の検証</item>
    ///   <item>楽観的ロック用の RowVersion</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique()
                .HasFilter("username IS NOT NULL");
            entity.HasIndex(u => u.Status)
                .HasDatabaseName("idx_users_status");
            entity.Property(u => u.Status)
                .HasConversion(new UpperSnakeCaseEnumConverter<UserStatus>())
                .HasMaxLength(50);
            entity.Property(u => u.Role)
                .HasConversion(new UpperSnakeCaseEnumConverter<UserRoleType>())
                .HasMaxLength(50);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_users_status",
                    "status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED')");
                t.HasCheckConstraint("ck_users_role",
                    "role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER')");
            });
            entity.Property(u => u.RowVersion).IsRowVersion();
            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(u => u.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    /// <summary>
    /// Role エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// 初期データとして 6 つの標準ロールを挿入します:
    /// ADMIN, MANAGER, STAFF, EMPLOYEE, USER, CUSTOMER
    /// </para>
    /// </remarks>
    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        // シードデータ用の固定日時（EF Core 10 の PendingModelChangesWarning 回避）
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_roles_name",
                    "name IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER')");
            });
            entity.HasData(
                new Role { Id = "role-admin", Name = "ADMIN", Description = "システム管理者", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Role { Id = "role-manager", Name = "MANAGER", Description = "店舗マネージャー", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Role { Id = "role-staff", Name = "STAFF", Description = "店舗スタッフ", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Role { Id = "role-employee", Name = "EMPLOYEE", Description = "従業員", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Role { Id = "role-user", Name = "USER", Description = "一般ユーザー", CreatedAt = seedDate, UpdatedAt = seedDate },
                new Role { Id = "role-customer", Name = "CUSTOMER", Description = "顧客", CreatedAt = seedDate, UpdatedAt = seedDate }
            );
        });
    }

    /// <summary>
    /// UserRole エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// ユーザーとロールの多対多関連を構成します。
    /// ユーザー削除時は CASCADE、ロール削除時は RESTRICT です。
    /// </para>
    /// </remarks>
    private static void ConfigureUserRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// UserSession エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// セッションの効率的なクエリのため、UserId + IsActive の複合インデックスを設定します。
    /// </para>
    /// </remarks>
    private static void ConfigureUserSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(s => s.SessionToken).IsUnique();
            entity.HasIndex(s => new { s.UserId, s.IsActive })
                .HasDatabaseName("idx_user_sessions_user_active");
            entity.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// OAuthAccount エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// Provider + ProviderUserId の複合一意制約により、
    /// 同一プロバイダーの同一ユーザーが重複して登録されることを防止します。
    /// </para>
    /// </remarks>
    private static void ConfigureOAuthAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OAuthAccount>(entity =>
        {
            entity.HasIndex(o => new { o.Provider, o.ProviderUserId }).IsUnique();
            entity.HasOne(o => o.User)
                .WithMany(u => u.OAuthAccounts)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// PasswordReset エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// トークンタイプは PASSWORD_RESET または EMAIL_VERIFICATION のいずれかです。
    /// </para>
    /// </remarks>
    private static void ConfigurePasswordReset(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.HasIndex(p => p.Token).IsUnique();
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_password_resets_token_type",
                    "token_type IN ('PASSWORD_RESET', 'EMAIL_VERIFICATION')");
            });
        });
    }

    /// <summary>
    /// UserMfa エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// ユーザーと MFA 設定は 1:1 のリレーションシップです。
    /// MFA 秘密鍵は暗号化して保存されます。
    /// </para>
    /// </remarks>
    private static void ConfigureUserMfa(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserMfa>(entity =>
        {
            entity.HasIndex(m => m.UserId).IsUnique();
            entity.HasOne(m => m.User)
                .WithOne(u => u.Mfa)
                .HasForeignKey<UserMfa>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// SecurityLog エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// セキュリティログはユーザー削除時も保持され、UserId は NULL に設定されます（SET NULL）。
    /// これにより、監査証跡を維持しながらユーザーの個人情報を削除できます。
    /// </para>
    /// </remarks>
    private static void ConfigureSecurityLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecurityLog>(entity =>
        {
            entity.HasIndex(s => s.UserId)
                .HasDatabaseName("idx_security_logs_user_id");
            entity.HasIndex(s => s.EventType)
                .HasDatabaseName("idx_security_logs_event_type");
            entity.HasIndex(s => s.CreatedAt)
                .HasDatabaseName("idx_security_logs_created_at");
            entity.HasOne(s => s.User)
                .WithMany(u => u.SecurityLogs)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// RefreshToken エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// FamilyId インデックスにより、トークンローテーション検知のための
    /// 効率的なファミリー検索が可能です。
    /// </para>
    /// </remarks>
    private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.Token).IsUnique();
            entity.HasIndex(r => r.FamilyId)
                .HasDatabaseName("idx_refresh_tokens_family_id");
            entity.HasIndex(r => new { r.UserId, r.IsRevoked })
                .HasDatabaseName("idx_refresh_tokens_user_active");
            entity.HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// OutboxEvent エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// Outbox パターンで使用されるイベントテーブルです。
    /// Status の CHECK 制約により、有効なステータス値のみが許可されます。
    /// </para>
    /// </remarks>
    private static void ConfigureOutboxEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(o => o.Status)
                .HasDatabaseName("idx_outbox_events_status");
            entity.Property(o => o.Status)
                .HasConversion(new UpperSnakeCaseEnumConverter<OutboxStatus>())
                .HasMaxLength(20);
            entity.HasIndex(o => o.CreatedAt)
                .HasDatabaseName("idx_outbox_events_created_at");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_outbox_events_status",
                    "status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')");
            });
        });
    }

    /// <summary>
    /// PasswordHistory エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// パスワード履歴は、パスワード再利用防止のために使用されます。
    /// 最新のパスワードから順にチェックするため、CreatedAt の降順インデックスを設定しています。
    /// </para>
    /// </remarks>
    private static void ConfigurePasswordHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.CreatedAt })
                .HasDatabaseName("idx_password_histories_user_id")
                .IsDescending(false, true);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// OAuthClient エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// OAuth クライアントは、外部アプリケーションからの OAuth 認可リクエストに使用されます。
    /// ClientId の一意制約により、クライアントの一意性が保証されます。
    /// </para>
    /// </remarks>
    private static void ConfigureOAuthClient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.HasIndex(o => o.ClientId).IsUnique();
        });
    }

    /// <summary>
    /// AuditLog エンティティの構成を行います。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    /// <remarks>
    /// <para>
    /// 監査ログは、エンティティの変更履歴を追跡するために使用されます。
    /// EntityType、ActorId、CreatedAt にインデックスを設定し、効率的なクエリを可能にしています。
    /// </para>
    /// </remarks>
    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.EntityType)
                .HasDatabaseName("idx_audit_logs_entity_type");
            entity.HasIndex(a => a.ActorId)
                .HasDatabaseName("idx_audit_logs_actor_id");
            entity.HasIndex(a => a.CreatedAt)
                .HasDatabaseName("idx_audit_logs_created_at");
        });
    }

    /// <summary>
    /// 変更をデータベースに保存します。
    /// </summary>
    /// <param name="cancellationToken">キャンセルを通知するトークン。</param>
    /// <returns>保存された変更の数。</returns>
    /// <remarks>
    /// <para>
    /// このオーバーライドでは、<see cref="IHasTimestamps"/> を実装するエンティティの
    /// タイムスタンプを自動的に設定します。また、SecurityLog、AuditLog、PasswordHistory の
    /// CreatedAt も自動設定されます。
    /// </para>
    /// <para>
    /// <strong>タイムスタンプの設定ルール:</strong>
    /// <list type="bullet">
    ///   <item>新規エンティティ（Added）: CreatedAt と UpdatedAt の両方を現在時刻に設定</item>
    ///   <item>更新エンティティ（Modified）: UpdatedAt のみを現在時刻に設定</item>
    /// </list>
    /// </para>
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is IHasTimestamps entity)
            {
                if (entry.State == EntityState.Added)
                    entity.CreatedAt = now;
                entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Added && entry.Entity is SecurityLog log)
                log.CreatedAt = now;

            if (entry.State == EntityState.Added && entry.Entity is AuditLog auditLog)
                auditLog.CreatedAt = now;

            if (entry.State == EntityState.Added && entry.Entity is PasswordHistory ph)
                ph.CreatedAt = now;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
