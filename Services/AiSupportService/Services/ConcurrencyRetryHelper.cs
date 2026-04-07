using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="DbUpdateConcurrencyException"/> 発生時にリトライを行うヘルパークラス。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、楽観的ロック（Optimistic Locking）を使用するデータベース操作において、
/// 同時更新競合が発生した場合に自動的にリトライを行います。
/// </para>
/// <para>
/// リトライ戦略:
/// <list type="bullet">
///   <item><description>指数バックオフ: 100ms × 試行回数（1回目: 100ms、2回目: 200ms、3回目: 300ms）</description></item>
///   <item><description>最大リトライ回数: デフォルト 3 回</description></item>
///   <item><description>最後のリトライでも失敗した場合は例外をスロー</description></item>
/// </list>
/// </para>
/// <para>
/// 使用例:
/// <code>
/// var result = await ConcurrencyRetryHelper.ExecuteWithRetryAsync(async () =>
/// {
///     entity.UpdatedAt = DateTime.UtcNow;
///     await context.SaveChangesAsync();
///     return entity;
/// });
/// </code>
/// </para>
/// </remarks>
public static class ConcurrencyRetryHelper
{
    /// <summary>
    /// 戻り値を持つ操作を楽観的ロック競合時にリトライ付きで実行する。
    /// </summary>
    /// <typeparam name="T">操作の戻り値の型。</typeparam>
    /// <param name="operation">
    /// 実行する非同期操作。<see cref="DbUpdateConcurrencyException"/> がスローされた場合にリトライされます。
    /// </param>
    /// <param name="maxRetries">
    /// 最大リトライ回数。デフォルトは 3。
    /// この回数に達しても競合が解消されない場合、例外がスローされます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 操作の実行結果。
    /// </returns>
    /// <exception cref="DbUpdateConcurrencyException">
    /// 最大リトライ回数に達しても競合が解消されない場合にスローされます。
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// キャンセルトークンがキャンセルされた場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 指数バックオフにより、競合が発生した場合でも
    /// 他のトランザクションが完了する時間を確保できます。
    /// </para>
    /// <para>
    /// 注意: この関数は操作を複数回実行する可能性があるため、
    /// 副作用のある操作（メール送信など）には適していません。
    /// </para>
    /// </remarks>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }
    }

    /// <summary>
    /// 戻り値を持たない操作を楽観的ロック競合時にリトライ付きで実行する。
    /// </summary>
    /// <param name="operation">
    /// 実行する非同期操作。<see cref="DbUpdateConcurrencyException"/> がスローされた場合にリトライされます。
    /// </param>
    /// <param name="maxRetries">
    /// 最大リトライ回数。デフォルトは 3。
    /// この回数に達しても競合が解消されない場合、例外がスローされます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 操作の完了を表すタスク。
    /// </returns>
    /// <exception cref="DbUpdateConcurrencyException">
    /// 最大リトライ回数に達しても競合が解消されない場合にスローされます。
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// キャンセルトークンがキャンセルされた場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 指数バックオフにより、競合が発生した場合でも
    /// 他のトランザクションが完了する時間を確保できます。
    /// </para>
    /// <para>
    /// 注意: この関数は操作を複数回実行する可能性があるため、
    /// 副作用のある操作（メール送信など）には適していません。
    /// </para>
    /// </remarks>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }
    }
}
