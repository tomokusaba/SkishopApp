using InventoryManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// 価格リポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// <para>一括無効化: DeactivateByProductIdAsync は ExecuteUpdateAsync で効率的にアクティブな価格を一括無効化する。</para>
/// <para>読み取り最適化: 参照系クエリは AsNoTracking で変更追跡を無効化する。</para>
/// </remarks>
public class PriceRepository(AppDbContext context) : IPriceRepository
{
    /// <inheritdoc />
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => await context.Database.BeginTransactionAsync(ct);

    /// <inheritdoc />
    public async Task<Price?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Prices
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public async Task<Price?> FindActiveByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Prices.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive, ct);

    /// <inheritdoc />
    /// <remarks>
    /// WHERE IN + IsActive フィルタで複数商品のアクティブ価格をバッチ取得する。
    /// AsNoTracking で変更追跡オーバーヘッドを排除する読み取り専用クエリ。
    /// </remarks>
    public async Task<List<Price>> FindActiveByProductIdsAsync(List<string> productIds, CancellationToken ct = default)
        => await context.Prices.AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId) && p.IsActive)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<Price>> FindByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Prices.AsNoTracking()
            .Where(p => p.ProductId == productId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<PriceHistory>> FindHistoryByProductIdAsync(
        string productId, int page, int size, CancellationToken ct = default)
        => await context.PriceHistories.AsNoTracking()
            .Where(h => h.ProductId == productId)
            .OrderByDescending(h => h.EffectiveDate)
            .Skip(page * size).Take(size)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<long> CountHistoryByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.PriceHistories.LongCountAsync(h => h.ProductId == productId, ct);

    /// <inheritdoc />
    public async Task AddAsync(Price price, CancellationToken ct = default)
        => await context.Prices.AddAsync(price, ct);

    /// <inheritdoc />
    public async Task AddHistoryAsync(PriceHistory history, CancellationToken ct = default)
        => await context.PriceHistories.AddAsync(history, ct);

    /// <inheritdoc />
    /// <remarks>
    /// ExecuteUpdateAsync を使用してアクティブな価格を一括で無効化する。
    /// エンティティを個別にロードせず、UPDATE 文を直接発行するため高効率。
    /// 新価格の登録前に呼び出すことで、アクティブ価格の排他性を保証する。
    /// </remarks>
    public async Task DeactivateByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Prices
            .Where(p => p.ProductId == productId && p.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, false), ct);

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
