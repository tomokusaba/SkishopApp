using InventoryManagementService.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// ExecutionStrategy ラッパー関数のデリゲート型。
/// NpgsqlRetryingExecutionStrategy 使用時にトランザクションをラップするために使用する。
/// </summary>
/// <param name="operation">トランザクション内で実行する操作</param>
/// <param name="ct">キャンセルトークン</param>
public delegate Task ExecuteInTransactionAsync(
    Func<CancellationToken, Task> operation, CancellationToken ct = default);

/// <summary>
/// 結果を返す ExecutionStrategy ラッパー関数のデリゲート型。
/// </summary>
/// <typeparam name="TResult">戻り値の型</typeparam>
public delegate Task<TResult> ExecuteInTransactionAsync<TResult>(
    Func<CancellationToken, Task<TResult>> operation, CancellationToken ct = default);

/// <summary>
/// 在庫リポジトリのインターフェース。Inventory Aggregate Root のデータアクセスを提供する。
/// </summary>
/// <remarks>
/// 悲観的ロック（SELECT FOR UPDATE）メソッドとトランザクション管理を含む。
/// デッドロック防止のため、複数商品のロック取得時は商品 ID 順にソートする。
/// </remarks>
public interface IInventoryRepository
{
    /// <summary>
    /// 商品 ID で在庫を取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫エンティティ。存在しない場合は null</returns>
    Task<Inventory?> FindByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で在庫を取得する（悲観的ロック: SELECT FOR UPDATE）。更新操作前に使用する。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ロック取得済みの在庫エンティティ。存在しない場合は null</returns>
    Task<Inventory?> FindByProductIdForUpdateAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 複数商品 ID で在庫を一括取得する（悲観的ロック: SELECT FOR UPDATE）。
    /// デッドロック防止のため商品 ID 順にソートしてロックを取得する。
    /// </summary>
    /// <param name="productIds">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ロック取得済みの在庫エンティティリスト</returns>
    Task<List<Inventory>> FindByProductIdsForUpdateAsync(List<string> productIds, CancellationToken ct = default);

    /// <summary>
    /// 在庫数が閾値以下の低在庫レコードを取得する（在庫数昇順、ページネーション対応）。
    /// </summary>
    /// <param name="threshold">低在庫閾値</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>低在庫の在庫リスト</returns>
    Task<List<Inventory>> FindLowStockAsync(int threshold, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// 在庫数が閾値以下の低在庫レコードの総件数を取得する。
    /// </summary>
    /// <param name="threshold">低在庫閾値</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>総件数</returns>
    Task<long> CountLowStockAsync(int threshold, CancellationToken ct = default);

    /// <summary>
    /// タイムアウトを超過した期限切れの引当レコードを取得する。引当解放バッチ処理用。
    /// </summary>
    /// <param name="timeout">引当タイムアウト期間</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>期限切れ引当のある在庫リスト</returns>
    Task<List<Inventory>> FindExpiredReservationsAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// 複数商品 ID で在庫を一括取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    /// <param name="productIds">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫エンティティリスト</returns>
    Task<List<Inventory>> FindByProductIdsAsync(List<string> productIds, CancellationToken ct = default);

    /// <summary>
    /// データベーストランザクションを開始する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>トランザクションオブジェクト</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// NpgsqlRetryingExecutionStrategy 互換のトランザクション実行を行う。
    /// CreateExecutionStrategy でラップされた操作内でトランザクションを安全に使用できる。
    /// </summary>
    /// <param name="operation">トランザクション内で実行する操作（トランザクションオブジェクトを引数として受け取る）</param>
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
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    Task SaveChangesAsync(CancellationToken ct = default);
}
