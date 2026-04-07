using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryManagementService.Repositories;

/// <summary>
/// 在庫リポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// <para>悲観的ロック: FindByProductIdForUpdateAsync は FromSqlInterpolated で SELECT FOR UPDATE を発行し、行レベルの排他ロックを取得する。</para>
/// <para>デッドロック防止: FindByProductIdsForUpdateAsync は商品 ID を昇順にソートしてからロックを順次取得する。</para>
/// <para>読み取り最適化: 参照系クエリは AsNoTracking で変更追跡オーバーヘッドを排除する。</para>
/// </remarks>
public class InventoryRepository(AppDbContext context) : IInventoryRepository
{
    /// <inheritdoc />
    public async Task<Inventory?> FindByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == productId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// FromSqlInterpolated で PostgreSQL の SELECT FOR UPDATE を発行し、行レベルの排他ロックを取得する。
    /// トランザクション内で使用すること。AsNoTracking は適用しない（変更追跡が必要なため）。
    /// </remarks>
    public async Task<Inventory?> FindByProductIdForUpdateAsync(string productId, CancellationToken ct = default)
        => await context.Inventories
            .FromSqlInterpolated($"SELECT * FROM inventories WHERE product_id = {productId} FOR UPDATE")
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>バッチ SELECT FOR UPDATE 戦略: PostgreSQL の ANY 演算子で複数商品 ID を一括取得しつつ、行ロックを取得する。</para>
    /// <para>デッドロック防止: 商品 ID を昇順ソート（ORDER BY product_id）することで、
    /// 複数トランザクションが同じ順序でロックを取得し、循環待ちを回避する。</para>
    /// </remarks>
    public async Task<List<Inventory>> FindByProductIdsForUpdateAsync(
        List<string> productIds, CancellationToken ct = default)
    {
        // ORDER BY product_id でデッドロック防止（H-06）、ANY でバッチ取得（C-06）
        var sortedIds = productIds.OrderBy(id => id).ToArray();
        return await context.Inventories
            .FromSqlInterpolated($"SELECT * FROM inventories WHERE product_id = ANY({sortedIds}) ORDER BY product_id FOR UPDATE")
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 在庫数が閾値以下かつ 0 より大きいレコードを抽出し、在庫数の昇順で返す。
    /// 完全に在庫切れ（quantity = 0）のレコードは除外される。
    /// </remarks>
    public async Task<List<Inventory>> FindLowStockAsync(
        int threshold, int page, int size, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .Where(i => i.Quantity <= threshold && i.Quantity > 0)
            .OrderBy(i => i.Quantity)
            .Skip(page * size).Take(size)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<long> CountLowStockAsync(int threshold, CancellationToken ct = default)
        => await context.Inventories.LongCountAsync(i => i.Quantity <= threshold && i.Quantity > 0, ct);

    /// <inheritdoc />
    /// <remarks>
    /// カットオフ時刻 = 現在時刻 − タイムアウト期間で算出し、
    /// ReservedAt がカットオフ以前かつ ReservedQuantity &gt; 0 のレコードを返す。
    /// 変更追跡を有効にして返すため、呼び出し側で引当解放後に SaveChangesAsync を実行すること。
    /// </remarks>
    public async Task<List<Inventory>> FindExpiredReservationsAsync(
        TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Add(-timeout);
        return await context.Inventories
            .Where(i => i.ReservedAt != null && i.ReservedAt < cutoff && i.ReservedQuantity > 0)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<Inventory>> FindByProductIdsAsync(
        List<string> productIds, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => await context.Database.BeginTransactionAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// NpgsqlRetryingExecutionStrategy と明示的トランザクションを組み合わせるための実装。
    /// CreateExecutionStrategy().ExecuteAsync() でリトライ可能な単位としてトランザクションをラップする。
    /// </remarks>
    public async Task ExecuteInTransactionAsync(
        Func<IDbContextTransaction, CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            await operation(transaction, ct);
        });
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IDbContextTransaction, CancellationToken, Task<TResult>> operation, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            return await operation(transaction, ct);
        });
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
