using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PointService.Models;

namespace PointService.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<PointAccount> PointAccounts => Set<PointAccount>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointExpiry> PointExpiries => Set<PointExpiry>();
    public DbSet<PointRule> PointRules => Set<PointRule>();
    public DbSet<PointCampaign> PointCampaigns => Set<PointCampaign>();
    public DbSet<PointConversionRate> PointConversionRates => Set<PointConversionRate>();
    public DbSet<TierDefinition> TierDefinitions => Set<TierDefinition>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<PointAuditLog> PointAuditLogs => Set<PointAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ValueConverter for string <-> Guid conversion (PostgreSQL uuid type)
        var stringToGuidConverter = new ValueConverter<string, Guid>(
            v => Guid.Parse(v),
            v => v.ToString());

        var nullableStringToGuidConverter = new ValueConverter<string?, Guid?>(
            v => v == null ? null : Guid.Parse(v),
            v => v == null ? null : v.ToString());

        modelBuilder.Entity<PointAccount>(entity =>
        {
            entity.HasIndex(a => a.UserId).IsUnique();
            entity.Property(a => a.AvailablePoints).HasDefaultValue(0);
            entity.Property(a => a.PendingPoints).HasDefaultValue(0);
            entity.Property(a => a.TotalEarned).HasDefaultValue(0);
            entity.Property(a => a.TotalSpent).HasDefaultValue(0);
            entity.Property(a => a.TotalExpired).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_accounts_available_points", "available_points >= 0"));
            entity.HasMany(a => a.Transactions)
                .WithOne(t => t.Account).HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(a => a.Expiries)
                .WithOne(e => e.Account).HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PointTransaction>(entity =>
        {
            entity.HasIndex(t => t.UserId).HasDatabaseName("idx_point_tx_user_id");
            entity.HasIndex(t => t.AccountId).HasDatabaseName("idx_point_tx_account_id");
            entity.HasIndex(t => new { t.ReferenceId, t.ReferenceType })
                .HasDatabaseName("idx_point_tx_reference");
            entity.HasIndex(t => t.CreatedAt).HasDatabaseName("idx_point_tx_created_at");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_transactions_type",
                "type IN ('EARN','REDEEM','EXPIRE','ADJUST','RESERVE','RELEASE','REFUND','CANCEL')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_transactions_points", "points != 0"));
        });

        modelBuilder.Entity<PointExpiry>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt })
                .HasDatabaseName("idx_point_expiry_user_expires")
                .HasFilter("status = 'ACTIVE'");
            entity.HasIndex(e => e.AccountId)
                .HasDatabaseName("idx_point_expiry_account_id");
            entity.Property(e => e.Status).HasDefaultValue(ExpiryStatuses.Active);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_expiries_status",
                "status IN ('ACTIVE','EXPIRED','CONSUMED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_expiries_points", "points > 0"));
        });

        modelBuilder.Entity<PointRule>(entity =>
        {
            entity.Property(r => r.Id).HasConversion(stringToGuidConverter);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.MinimumAmount).HasDefaultValue(0m);
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_rules_minimum_amount", "minimum_amount >= 0"));
        });

        modelBuilder.Entity<PointCampaign>(entity =>
        {
            entity.Property(c => c.Id).HasConversion(stringToGuidConverter);
            entity.HasIndex(c => new { c.StartDate, c.EndDate })
                .HasDatabaseName("idx_campaign_active_dates")
                .HasFilter("is_active = true");
            entity.Property(c => c.Multiplier).HasDefaultValue(1.0m);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_campaigns_multiplier", "multiplier > 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_campaigns_dates", "end_date > start_date"));
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.Property(e => e.Id).HasConversion(stringToGuidConverter);
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_pending")
                .HasFilter("status = 'PENDING'");
            entity.Property(e => e.Status).HasDefaultValue(OutboxStatuses.Pending);
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_status",
                "status IN ('PENDING','PUBLISHED','FAILED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_retry_count", "retry_count >= 0"));
        });

        modelBuilder.Entity<TierDefinition>(entity =>
        {
            entity.Property(t => t.Id).HasConversion(stringToGuidConverter);
            entity.HasIndex(t => t.Name).IsUnique();
            entity.HasIndex(t => t.SortOrder);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_tier_definitions_name",
                "name IN ('BRONZE','SILVER','GOLD','PLATINUM')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_tier_definitions_point_rate",
                "point_rate > 0 AND point_rate <= 1"));
        });

        modelBuilder.Entity<PointConversionRate>(entity =>
        {
            entity.Property(r => r.Id).HasConversion(stringToGuidConverter);
            entity.HasIndex(r => r.CurrencyCode).IsUnique();
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_conversion_rates_rate",
                "rate_per_point > 0"));
        });

        modelBuilder.Entity<PointAuditLog>(entity =>
        {
            entity.Property(a => a.Id).HasConversion(stringToGuidConverter);
            entity.Property(a => a.AdminUserId).HasConversion(stringToGuidConverter);
            entity.Property(a => a.TargetUserId).HasConversion(stringToGuidConverter);
            entity.HasIndex(a => a.AdminUserId)
                .HasDatabaseName("idx_audit_admin_user_id");
            entity.HasIndex(a => a.TargetUserId)
                .HasDatabaseName("idx_audit_target_user_id");
            entity.HasIndex(a => a.CreatedAt)
                .HasDatabaseName("idx_audit_created_at");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_audit_logs_action",
                "action IN ('ADD','SUBTRACT','ADJUST')"));
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is PointAccount account)
            {
                if (entry.State == EntityState.Added) account.CreatedAt = now;
                account.UpdatedAt = now;
            }
            else if (entry.Entity is PointExpiry expiry)
            {
                if (entry.State == EntityState.Added) expiry.CreatedAt = now;
                expiry.UpdatedAt = now;
            }
            else if (entry.Entity is PointRule rule)
            {
                if (entry.State == EntityState.Added) rule.CreatedAt = now;
                rule.UpdatedAt = now;
            }
            else if (entry.Entity is PointCampaign campaign)
            {
                if (entry.State == EntityState.Added) campaign.CreatedAt = now;
                campaign.UpdatedAt = now;
            }
            else if (entry.Entity is PointConversionRate rate)
            {
                if (entry.State == EntityState.Added) rate.CreatedAt = now;
                rate.UpdatedAt = now;
            }
            else if (entry.Entity is TierDefinition tier)
            {
                if (entry.State == EntityState.Added) tier.CreatedAt = now;
                tier.UpdatedAt = now;
            }
            else if (entry.Entity is PointTransaction tx && entry.State == EntityState.Added)
            {
                tx.CreatedAt = now;
            }
            else if (entry.Entity is OutboxEvent outbox && entry.State == EntityState.Added)
            {
                outbox.CreatedAt = now;
            }
            else if (entry.Entity is PointAuditLog auditLog && entry.State == EntityState.Added)
            {
                auditLog.CreatedAt = now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
