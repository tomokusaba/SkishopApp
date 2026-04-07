using InventoryManagementService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Infrastructure.Persistence;

/// <summary>
/// 在庫管理サービスの EF Core DbContext。
/// 全エンティティの永続化とモデル構成を管理する。
/// </summary>
/// <param name="options">DbContext 構成オプション</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー（CreatedAt / UpdatedAt の自動設定に使用）</param>
/// <remarks>
/// SaveChangesAsync をオーバーライドし、TimeProvider 経由で
/// CreatedAt（追加時）と UpdatedAt（追加・更新時）を自動設定する。
/// OnModelCreating で全エンティティのインデックス、一意制約、外部キー、
/// CHECK 制約、デフォルト値を定義する。
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider) : DbContext(options)
{
    /// <summary>商品テーブル</summary>
    public DbSet<Product> Products => Set<Product>();
    /// <summary>カテゴリテーブル</summary>
    public DbSet<Category> Categories => Set<Category>();
    /// <summary>在庫テーブル</summary>
    public DbSet<Inventory> Inventories => Set<Inventory>();
    /// <summary>価格テーブル</summary>
    public DbSet<Price> Prices => Set<Price>();
    /// <summary>価格履歴テーブル</summary>
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    /// <summary>商品画像テーブル</summary>
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    /// <summary>レビューテーブル</summary>
    public DbSet<Review> Reviews => Set<Review>();
    /// <summary>レビュー返信テーブル</summary>
    public DbSet<ReviewResponse> ReviewResponses => Set<ReviewResponse>();
    /// <summary>レビュー投票テーブル</summary>
    public DbSet<ReviewVote> ReviewVotes => Set<ReviewVote>();
    /// <summary>サプライヤーテーブル</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    /// <summary>商品-サプライヤー中間テーブル</summary>
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    /// <summary>サイズガイドテーブル</summary>
    public DbSet<SizeGuide> SizeGuides => Set<SizeGuide>();
    /// <summary>Outbox イベントテーブル（Outbox パターン用）</summary>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    /// <summary>処理済みメッセージテーブル（冪等性保証用）</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    /// <summary>
    /// エンティティモデルの構成を定義する。
    /// インデックス、一意制約、外部キー、CHECK 制約、デフォルト値を設定する。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー</param>
    /// <remarks>
    /// <para>各エンティティに対して以下の Fluent API 構成を適用する:</para>
    /// <list type="bullet">
    ///   <item><description>Product: SKU 一意制約、CategoryId / Brand / IsActive / CreatedAt インデックス、重量 CHECK 制約（0 以上）、カテゴリ削除時 Restrict</description></item>
    ///   <item><description>Category: 自己参照（親子階層）、親カテゴリ削除時 Restrict</description></item>
    ///   <item><description>Inventory: 複合インデックス（ProductId + LocationCode）、数量・予約数量の CHECK 制約（quantity ≥ reserved_quantity ≥ 0）、予約日の部分インデックス</description></item>
    ///   <item><description>Price: 複合インデックス（ProductId + IsActive, SaleStartDate + SaleEndDate）、価格の精度（12,2）と CHECK 制約</description></item>
    ///   <item><description>PriceHistory: 価格種別の CHECK 制約（REGULAR / SALE / CLEARANCE）</description></item>
    ///   <item><description>Review: ProductId + UserId の一意制約（1 ユーザー 1 商品 1 レビュー）、RowVersion による楽観的ロック</description></item>
    ///   <item><description>ReviewVote: UserId + ReviewId の一意制約（重複投票防止）</description></item>
    ///   <item><description>OutboxEvent: ステータス別の部分インデックス（PENDING / FAILED）で Outbox ポーリング性能を最適化</description></item>
    /// </list>
    /// <para>全エンティティの CreatedAt / UpdatedAt に DB デフォルト値 CURRENT_TIMESTAMP を設定する。</para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.Brand);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt).IsDescending();
            entity.HasOne(e => e.Category).WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Weight).HasPrecision(8, 2);
            entity.ToTable(t => t.HasCheckConstraint("ck_products_weight", "weight IS NULL OR weight >= 0"));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(e => e.Parent).WithMany(c => c.Children)
                .HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.LocationCode);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.ProductId, e.LocationCode });
            entity.HasOne(e => e.Product).WithMany(p => p.Inventories)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_inventory_quantity", "quantity >= 0");
                t.HasCheckConstraint("ck_inventory_reserved_quantity", "reserved_quantity >= 0");
                t.HasCheckConstraint("ck_inventory_quantity_reserved", "quantity >= reserved_quantity");
                t.HasCheckConstraint("ck_inventory_reorder_point", "reorder_point >= 0");
                // H-4: ステータス CHECK 制約追加
                t.HasCheckConstraint("ck_inventory_status", "status IN ('IN_STOCK', 'LOW_STOCK', 'OUT_OF_STOCK', 'RESERVED', 'DISCONTINUED')");
            });
            entity.HasIndex(e => e.ReservedAt)
                .HasFilter("reserved_at IS NOT NULL")
                .HasDatabaseName("ix_inventory_reserved_at_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Price>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.IsActive });
            entity.HasIndex(e => new { e.SaleStartDate, e.SaleEndDate });
            entity.HasOne(e => e.Product).WithMany(p => p.Prices)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.RegularPrice).HasPrecision(12, 2);
            entity.Property(e => e.SalePrice).HasPrecision(12, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_prices_regular_price", "regular_price >= 0");
                t.HasCheckConstraint("ck_prices_sale_price", "sale_price >= 0");
            });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.EffectiveDate);
            entity.HasOne(e => e.Product).WithMany()
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Price).HasPrecision(12, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_price_histories_price", "price >= 0");
                // H-2: 設計書に合わせて CLEARANCE → PROMOTION に変更
                t.HasCheckConstraint("ck_price_history_price_type", "price_type IN ('REGULAR', 'SALE', 'PROMOTION')");
            });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasOne(e => e.Product).WithMany(p => p.Images)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            // H-3: DETAIL タイプを追加
            entity.ToTable(t => t.HasCheckConstraint("ck_product_images_type", "type IN ('MAIN', 'GALLERY', 'THUMBNAIL', 'DETAIL')"));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Product).WithMany(p => p.Reviews)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_reviews_rating", "rating >= 1 AND rating <= 5");
                t.HasCheckConstraint("ck_reviews_status", "status IN ('PENDING', 'APPROVED', 'REJECTED')");
            });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ReviewResponse>(entity =>
        {
            entity.HasOne(e => e.Review).WithMany(r => r.Responses)
                .HasForeignKey(e => e.ReviewId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ReviewVote>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ReviewId })
                .IsUnique()
                .HasDatabaseName("uq_review_votes_user_review");
            entity.HasOne(e => e.Review)
                .WithMany()
                .HasForeignKey(e => e.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<SizeGuide>(entity =>
        {
            entity.HasIndex(e => e.CategoryId);
            entity.HasOne(e => e.Category).WithMany(c => c.SizeGuides)
                .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ProductSupplier>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.SupplierId });
            entity.HasOne(e => e.Product).WithMany()
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Supplier).WithMany(s => s.ProductSuppliers)
                .HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.SupplierPrice).HasPrecision(12, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasIndex(e => e.Topic);
            entity.HasIndex(e => e.ProcessedAt);
            entity.Property(e => e.ProcessedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasFilter("status = 'PENDING'")
                .HasDatabaseName("ix_outbox_events_pending");
            entity.HasIndex(e => new { e.Status, e.RetryCount })
                .HasFilter("status = 'FAILED'")
                .HasDatabaseName("ix_outbox_events_failed");
            // C-1: PROCESSING ステータスを追加（マージブロッカー修正）
            entity.ToTable(t => t.HasCheckConstraint("ck_outbox_events_status", "status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')"));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    /// <summary>
    /// 変更保存時に CreatedAt / UpdatedAt を TimeProvider 経由で自動設定する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>保存された状態エントリの数</returns>
    /// <remarks>
    /// Added 状態のエンティティには CreatedAt と UpdatedAt を設定し、
    /// Modified 状態のエンティティには UpdatedAt のみを更新する。
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var updatedAtProp = entry.Metadata.FindProperty("UpdatedAt");
            if (updatedAtProp is not null)
            {
                // エンティティのプロパティ型に応じて適切な値を設定
                entry.Property("UpdatedAt").CurrentValue = updatedAtProp.ClrType == typeof(DateTimeOffset)
                    ? now
                    : now.UtcDateTime;
            }

            if (entry.State == EntityState.Added)
            {
                var createdAtProp = entry.Metadata.FindProperty("CreatedAt");
                if (createdAtProp is not null)
                {
                    entry.Property("CreatedAt").CurrentValue = createdAtProp.ClrType == typeof(DateTimeOffset)
                        ? now
                        : now.UtcDateTime;
                }
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
