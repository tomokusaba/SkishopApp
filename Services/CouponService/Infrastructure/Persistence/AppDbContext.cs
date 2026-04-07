using CouponService.Models;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CouponType> CouponTypes => Set<CouponType>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRestriction> CouponRestrictions => Set<CouponRestriction>();
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.DiscountValue).HasPrecision(12, 2);
            entity.Property(e => e.MaxDiscountAmount).HasPrecision(12, 2);
            entity.Property(e => e.MinOrderAmount).HasPrecision(12, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CouponType>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.Property(e => e.DiscountAmount).HasPrecision(12, 2);
            entity.HasIndex(e => new { e.CouponId, e.UserId });
            entity.HasIndex(e => new { e.CouponId, e.OrderId }).IsUnique();
        });

        modelBuilder.Entity<UserCoupon>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CouponId }).IsUnique();
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.Status)
                .HasFilter("\"status\" = 0");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CouponRestriction>(entity =>
        {
            entity.Property(e => e.PercentageMax).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = timeProvider.GetUtcNow();

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
