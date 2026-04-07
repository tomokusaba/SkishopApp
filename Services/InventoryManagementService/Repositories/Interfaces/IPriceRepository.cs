using InventoryManagementService.Models;

using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// 価格リポジトリのインターフェース。Price エンティティおよび PriceHistory のデータアクセスを提供する。
/// </summary>
public interface IPriceRepository
{
    /// <summary>
    /// データベーストランザクションを開始する。
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// NpgsqlRetryingExecutionStrategy 互換のトランザクション実行を行う。
    /// CreateExecutionStrategy でラップされた操作内でトランザクションを安全に使用できる。
    /// </summary>
    /// <param name="operation">トランザクション内で実行する操作</param>
    /// <param name="ct">キャンセルトークン</param>
    Task ExecuteInTransactionAsync(
        Func<IDbContextTransaction, CancellationToken, Task> operation, CancellationToken ct = default);

    /// <summary>
    /// NpgsqlRetryingExecutionStrategy 互換のトランザクション実行を行い、結果を返す。
    /// </summary>
    /// <typeparam name="TResult">戻り値の型</typeparam>
    /// <param name="operation">トランザクション内で実行する操作</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>操作の結果</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IDbContextTransaction, CancellationToken, Task<TResult>> operation, CancellationToken ct = default);

    /// <summary>
    /// 価格 ID で価格レコードを取得する。
    /// </summary>
    Task<Price?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID でアクティブな価格を取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    Task<Price?> FindActiveByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 複数商品 ID でアクティブな価格を一括取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    Task<List<Price>> FindActiveByProductIdsAsync(List<string> productIds, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で全価格レコードを取得する（作成日降順、読み取り専用）。
    /// </summary>
    Task<List<Price>> FindByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 商品の価格変更履歴を取得する（適用日降順、ページネーション対応、読み取り専用）。
    /// </summary>
    Task<List<PriceHistory>> FindHistoryByProductIdAsync(string productId, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// 商品の価格変更履歴の総件数を取得する。
    /// </summary>
    Task<long> CountHistoryByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 価格エンティティを追加する。
    /// </summary>
    Task AddAsync(Price price, CancellationToken ct = default);

    /// <summary>
    /// 価格変更履歴を追加する。
    /// </summary>
    Task AddHistoryAsync(PriceHistory history, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID のアクティブな価格を一括で無効化する（ExecuteUpdateAsync による効率的な一括更新）。
    /// </summary>
    Task DeactivateByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
