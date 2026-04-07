using Microsoft.EntityFrameworkCore;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();
    internal DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    internal DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Cart ──
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(c => c.Items).AutoInclude();

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_carts_session_id");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("idx_carts_customer_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_carts_status");
            entity.HasIndex(e => new { e.CustomerId, e.Status })
                .HasDatabaseName("idx_carts_customer_status");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("idx_carts_expired")
                .HasFilter("status = 'Active' AND expires_at IS NOT NULL");

            entity.Property(e => e.RowVersion)
                .HasDefaultValue(Array.Empty<byte>())
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_carts_status",
                "status IN ('Active', 'Expired', 'Abandoned', 'CheckedOut')"));
        });

        // ── CartItem ──
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(e => new { e.CartId, e.ProductId })
                .IsUnique()
                .HasDatabaseName("idx_cart_items_unique");

            entity.HasIndex(e => e.CartId).HasDatabaseName("idx_cart_items_cart_id");
            entity.HasIndex(e => e.ProductId).HasDatabaseName("idx_cart_items_product_id");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(12,2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(12,2)");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_cart_items_quantity", "quantity > 0"));
        });

        // ── Payment ──
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasMany(p => p.Transactions)
                .WithOne(t => t.Payment)
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Navigation(p => p.Transactions).AutoInclude();

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.OrderId).IsUnique().HasDatabaseName("idx_payments_order_id");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("idx_payments_customer_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_payments_status");
            entity.HasIndex(e => e.StripePaymentIntentId)
                .HasDatabaseName("idx_payments_stripe_intent_id");
            entity.HasIndex(e => e.StripeCheckoutSessionId)
                .HasDatabaseName("idx_payments_stripe_session_id");

            entity.Property(e => e.RowVersion)
                .HasDefaultValue(Array.Empty<byte>())
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_payments_amount", "amount > 0");
                t.HasCheckConstraint("ck_payments_status",
                    "status IN ('Pending', 'Processing', 'Completed', 'Failed', 'Refunded', 'Cancelled')");
            });
        });

        // ── Transaction ──
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");

            entity.HasIndex(e => e.PaymentId)
                .HasDatabaseName("idx_transactions_payment_id");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_transactions_amount", "amount >= 0"));
        });

        // ── PaymentMethod ──
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>();

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_payment_methods_user_id");
        });

        // ── OutboxEvent ──
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.Status, e.RetryCount })
                .HasDatabaseName("idx_outbox_events_failed")
                .HasFilter("status = 'Failed'");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_events_pending")
                .HasFilter("status = 'Pending'");

            // AggregateId インデックス（特定 Aggregate のイベント検索用）
            entity.HasIndex(e => e.AggregateId)
                .HasDatabaseName("idx_outbox_events_aggregate_id");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_outbox_status",
                    "status IN ('Pending', 'Processing', 'Published', 'Failed', 'DeadLetter')");
                t.HasCheckConstraint("ck_outbox_retry_count", "retry_count >= 0");
            });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries<IHasTimestamps>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;

            entry.Entity.UpdatedAt = now;
        }

        // Handle row version for new entities
        var cartEntries = ChangeTracker.Entries<Cart>()
            .Where(e => e.State == EntityState.Added);
        
        foreach (var entry in cartEntries)
        {
            if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        var paymentEntries = ChangeTracker.Entries<Payment>()
            .Where(e => e.State == EntityState.Added);
        
        foreach (var entry in paymentEntries)
        {
            if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        return await base.SaveChangesAsync(ct);
    }
}
