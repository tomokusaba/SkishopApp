namespace InventoryManagementService.Exceptions;

/// <summary>
/// 楽観的ロックの競合が検出された場合にスローされる例外。HTTP 409 Conflict にマッピングされる。
/// EF Core の <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> をラップして使用する。
/// </summary>
/// <param name="message">競合の詳細メッセージ。</param>
public class ConcurrencyException(string message)
    : InventoryException("CONCURRENCY_CONFLICT", message);
