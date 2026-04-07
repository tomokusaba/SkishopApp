using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace SalesManagementService.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL Advisory Lock 操作用の DatabaseFacade 拡張メソッド。
/// ADO.NET を直接使用し、EF Core の SqlQuery オーバーヘッドを回避。
/// </summary>
public static class DatabaseFacadeAdvisoryLockExtensions
{
    /// <summary>
    /// PostgreSQL の pg_try_advisory_lock を使用してセッションレベルの排他ロックを試行取得する。
    /// </summary>
    /// <param name="database">DatabaseFacade インスタンス</param>
    /// <param name="lockName">ロック名（hashtext でハッシュ化される）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ロック取得成功時は true、既に他セッションがロック保持中の場合は false</returns>
    public static async Task<bool> TryAcquireAdvisoryLockAsync(
        this DatabaseFacade database,
        string lockName,
        CancellationToken ct = default)
    {
        var connection = database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = (NpgsqlCommand)connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(hashtext($1))";
            command.Parameters.Add(new NpgsqlParameter { Value = lockName });

            var result = await command.ExecuteScalarAsync(ct);
            return result is true;
        }
        finally
        {
            // 接続を開いた場合のみクローズ（EF Core の接続管理を尊重）
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// PostgreSQL の pg_advisory_unlock を使用してセッションレベルの排他ロックを解放する。
    /// </summary>
    /// <param name="database">DatabaseFacade インスタンス</param>
    /// <param name="lockName">ロック名（hashtext でハッシュ化される）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ロック解放成功時は true、ロックを保持していなかった場合は false</returns>
    public static async Task<bool> ReleaseAdvisoryLockAsync(
        this DatabaseFacade database,
        string lockName,
        CancellationToken ct = default)
    {
        var connection = database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = (NpgsqlCommand)connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(hashtext($1))";
            command.Parameters.Add(new NpgsqlParameter { Value = lockName });

            var result = await command.ExecuteScalarAsync(ct);
            return result is true;
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }
}
