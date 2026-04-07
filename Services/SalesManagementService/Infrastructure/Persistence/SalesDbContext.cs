using Microsoft.EntityFrameworkCore;
using SalesManagementService.Models;

namespace SalesManagementService.Infrastructure.Persistence;

public class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<SagaLog> SagaLogs => Set<SagaLog>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Order ──
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.OrderDate);

            entity.Property(o => o.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(o => o.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_orders_status",
                    "status IN ('PENDING','CONFIRMED','PROCESSING','SHIPPED','DELIVERED','RETURNED','REFUNDED','CANCELLED','INVENTORY_SHORTAGE','PAYMENT_FAILED','PENDING_PAYMENT')");
                t.HasCheckConstraint(
                    "ck_orders_payment_status",
                    "payment_status IN ('PENDING','AUTHORIZED','CAPTURED','FAILED','REFUNDED','PARTIALLY_REFUNDED')");
                t.HasCheckConstraint("ck_orders_subtotal_amount", "subtotal_amount >= 0");
                t.HasCheckConstraint("ck_orders_tax_amount", "tax_amount >= 0");
                t.HasCheckConstraint("ck_orders_shipping_fee", "shipping_fee >= 0");
                t.HasCheckConstraint("ck_orders_discount_amount", "discount_amount >= 0");
                t.HasCheckConstraint("ck_orders_total_amount", "total_amount >= 0");
                t.HasCheckConstraint("ck_orders_used_points", "used_points >= 0");
                t.HasCheckConstraint("ck_orders_point_discount_amount", "point_discount_amount >= 0");
            });

            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.Shipments)
                .WithOne(s => s.Order)
                .HasForeignKey(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.Returns)
                .WithOne(r => r.Order)
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Invoice)
                .WithOne(i => i.Order)
                .HasForeignKey<Invoice>(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.SagaLog)
                .WithOne(s => s.Order)
                .HasForeignKey<SagaLog>(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OrderItem ──
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(i => i.ProductId);
            entity.Property(i => i.ProductSnapshot).HasColumnType("jsonb");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_order_items_unit_price", "unit_price >= 0");
                t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_order_items_subtotal", "subtotal >= 0");
                t.HasCheckConstraint("ck_order_items_coupon_discount_amount", "coupon_discount_amount >= 0");
                t.HasCheckConstraint("ck_order_items_used_points", "used_points >= 0");
                t.HasCheckConstraint("ck_order_items_point_discount_amount", "point_discount_amount >= 0");
            });
        });

        // ── Shipment ──
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(s => s.OrderId).IsUnique();
            entity.HasIndex(s => s.TrackingNumber);
            entity.HasIndex(s => s.Status);

            entity.Property(s => s.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(s => s.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_shipments_status",
                    "status IN ('PREPARING','SHIPPED','IN_TRANSIT','DELIVERED','FAILED')");
            });
        });

        // ── Return ──
        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasIndex(r => r.ReturnNumber).IsUnique();
            entity.HasIndex(r => r.OrderId);
            entity.HasIndex(r => r.Status);

            entity.Property(r => r.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(r => r.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(r => r.OrderItem)
                .WithMany()
                .HasForeignKey(r => r.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_returns_reason",
                    "reason IN ('DEFECTIVE','WRONG_ITEM','SIZE_MISMATCH','NOT_AS_DESCRIBED','CHANGED_MIND','OTHER')");
                t.HasCheckConstraint(
                    "ck_returns_status",
                    "status IN ('REQUESTED','APPROVED','REJECTED','RECEIVED','REFUNDED','CLOSED')");
                t.HasCheckConstraint("ck_returns_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_returns_refund_amount", "refund_amount >= 0");
            });
        });

        // ── Invoice ──
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();

            entity.Property(i => i.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(i => i.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_invoices_status",
                    "status IN ('DRAFT','ISSUED','PAID','OVERDUE','CANCELLED')");
                t.HasCheckConstraint("ck_invoices_amount", "amount >= 0");
            });
        });

        // ── SagaLog ──
        modelBuilder.Entity<SagaLog>(entity =>
        {
            entity.HasIndex(s => s.OrderId);
            entity.HasIndex(s => s.UpdatedAt)
                .HasFilter("status = 'PROCESSING'");
            entity.HasIndex(s => s.UpdatedAt)
                .HasDatabaseName("idx_saga_logs_pending_payment")
                .HasFilter("status = 'PENDING_PAYMENT'");

            entity.Property(s => s.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(s => s.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(s => s.StepResults).HasColumnType("jsonb");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_saga_logs_saga_type",
                    "saga_type IN ('ORDER_CHECKOUT','ORDER_CANCEL','ORDER_RETURN')");
                t.HasCheckConstraint(
                    "ck_saga_logs_status",
                    "status IN ('CREATED','PROCESSING','COMPLETED','COMPENSATING','COMPENSATED','FAILED','PENDING_PAYMENT')");
                t.HasCheckConstraint("ck_saga_logs_current_step", "current_step >= 0");
                t.HasCheckConstraint("ck_saga_logs_retry_count", "retry_count >= 0");
            });
        });

        // ── OutboxEvent ──
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt)
                .HasFilter("status = 'PENDING'");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_outbox_events_status",
                    "status IN ('PENDING','PROCESSING','PUBLISHED','FAILED','DEAD_LETTER')");
                t.HasCheckConstraint("ck_outbox_events_retry_count", "retry_count >= 0");
            });
        });

        // ── IdempotencyKey ──
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasIndex(k => new { k.Key, k.UserId }).IsUnique();

            entity.Property(k => k.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(k => k.ResponseBody).HasColumnType("jsonb");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_idempotency_keys_request_status",
                    "request_status IN ('PENDING','PROCESSING','COMPLETED')");
            });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case Order order:
                    if (entry.State == EntityState.Added) order.CreatedAt = now;
                    order.UpdatedAt = now;
                    break;
                case Shipment shipment:
                    if (entry.State == EntityState.Added) shipment.CreatedAt = now;
                    shipment.UpdatedAt = now;
                    break;
                case Return ret:
                    if (entry.State == EntityState.Added) ret.CreatedAt = now;
                    ret.UpdatedAt = now;
                    break;
                case Invoice invoice:
                    if (entry.State == EntityState.Added) invoice.CreatedAt = now;
                    invoice.UpdatedAt = now;
                    break;
                case SagaLog saga:
                    if (entry.State == EntityState.Added) saga.CreatedAt = now;
                    saga.UpdatedAt = now;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
