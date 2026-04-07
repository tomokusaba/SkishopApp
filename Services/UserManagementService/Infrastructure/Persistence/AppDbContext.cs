using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserManagementService.Exceptions;
using UserManagementService.Models;

namespace UserManagementService.Infrastructure.Persistence;

/// <summary>
/// UserManagementService の EF Core DbContext。
/// SaveChangesAsync をオーバーライドし、CreatedAt/UpdatedAt の自動設定と
/// <see cref="ConcurrencyException"/> への変換を行う。
/// </summary>
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory) : DbContext(options)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AppDbContext>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<MemberRank> MemberRanks => Set<MemberRank>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<DeletionRequest> DeletionRequests => Set<DeletionRequest>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    // ── EF Core Fluent API によるモデル構成 ──
    // インデックス、リレーションシップ、CHECK 制約を定義する。
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Status);
            entity.HasIndex(u => u.CreatedAt);

            entity.HasOne(u => u.Preference)
                .WithOne(p => p.User)
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.MemberRank)
                .WithOne(m => m.User)
                .HasForeignKey<MemberRank>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Addresses)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Wishlists)
                .WithOne(w => w.User)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Activities)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Consents)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.DeletionRequests)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_users_status",
                "status IN ('PENDING_VERIFICATION','ACTIVE','SUSPENDED','DEACTIVATED')"));
        });

        // ── Address ──
        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.IsDefault });

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_addresses_address_type",
                "address_type IN ('SHIPPING','BILLING')"));
        });

        // ── Wishlist / WishlistItem ──
        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasIndex(w => w.UserId);

            entity.HasMany(w => w.Items)
                .WithOne(i => i.Wishlist)
                .HasForeignKey(i => i.WishlistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.HasIndex(i => i.WishlistId);
        });

        // ── UserPreference ──
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        // ── UserActivity ──
        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.ActivityType });
        });

        // ── MemberRank ──
        modelBuilder.Entity<MemberRank>(entity =>
        {
            entity.HasIndex(m => m.UserId).IsUnique();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_member_ranks_current_rank",
                    "current_rank IN ('BRONZE','SILVER','GOLD','PLATINUM')");
                t.HasCheckConstraint(
                    "ck_member_ranks_annual_purchase_amount",
                    "annual_purchase_amount >= 0");
                t.HasCheckConstraint(
                    "ck_member_ranks_point_rate",
                    "point_rate >= 0 AND point_rate <= 1");
            });
        });

        // ── Consent ──
        modelBuilder.Entity<Consent>(entity =>
        {
            entity.HasIndex(c => c.UserId);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_consents_consent_type",
                    "consent_type IN ('MARKETING','PERSONALIZATION','ANALYTICS','THIRD_PARTY_SHARING')");
            });
        });

        // ── DeletionRequest ──
        modelBuilder.Entity<DeletionRequest>(entity =>
        {
            entity.HasIndex(d => d.UserId);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_deletion_requests_status",
                    "status IN ('PENDING','PROCESSING','COMPLETED','FAILED','CANCELLED','AWAITING_MANUAL_INTERVENTION')");
                t.HasCheckConstraint(
                    "ck_deletion_requests_request_channel",
                    "request_channel IN ('WEB_SELF_SERVICE','ADMIN_CONSOLE','EMAIL_DSR','API')");
            });
        });

        // ── OutboxEvent ──
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_outbox_events_status_pending")
                .HasFilter("status = 'PENDING'");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_outbox_events_status_failed")
                .HasFilter("status = 'FAILED'");
        });

        // ── ProcessedEvent ──
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasIndex(e => new { e.EventId, e.EventType }).IsUnique();
        });
    }

    /// <summary>
    /// SaveChangesAsync をオーバーライドし、追加/変更エンティティの CreatedAt/UpdatedAt を自動設定する。
    /// <see cref="DbUpdateConcurrencyException"/> を <see cref="ConcurrencyException"/> に変換する。
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is User user)
            {
                if (entry.State == EntityState.Added)
                    user.CreatedAt = now;
                user.UpdatedAt = now;
            }
            else if (entry.Entity is Address address)
            {
                if (entry.State == EntityState.Added)
                    address.CreatedAt = now;
                address.UpdatedAt = now;
            }
            else if (entry.Entity is UserPreference preference)
            {
                if (entry.State == EntityState.Added)
                    preference.CreatedAt = now;
                preference.UpdatedAt = now;
            }
            else if (entry.Entity is Consent consent)
            {
                if (entry.State == EntityState.Added)
                    consent.CreatedAt = now;
                consent.UpdatedAt = now;
            }
            else if (entry.Entity is Wishlist wishlist)
            {
                if (entry.State == EntityState.Added)
                    wishlist.CreatedAt = now;
                wishlist.UpdatedAt = now;
            }
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityType = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "Unknown";
            _logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", entityType);
            throw new ConcurrencyException(
                "データが他のユーザーによって更新されました。再度お試しください。");
        }
    }
}
