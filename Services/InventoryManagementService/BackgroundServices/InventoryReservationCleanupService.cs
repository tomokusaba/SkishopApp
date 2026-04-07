using InventoryManagementService.Events;
using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// 在庫予約のタイムアウトクリーンアップ BackgroundService。
/// 15 分以上経過した未確定の在庫引当を自動的に解放する。
/// </summary>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - 60 秒間隔でポーリング実行
/// - pg_try_advisory_lock(12346) で複数インスタンス間の排他制御を行う
/// - 予約タイムアウト: 15 分（ReservationTimeout）
/// - 解放時に InventoryReleased イベントを Outbox 経由で発行
/// - タイムアウト解放は補償トランザクションの一部として機能する
/// </remarks>
public class InventoryReservationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryReservationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 期限切れ在庫予約のクリーンアップループを実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InventoryReservationCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var inventoryRepository = scope.ServiceProvider
                    .GetRequiredService<IInventoryRepository>();
                var eventPublisher = scope.ServiceProvider
                    .GetRequiredService<IEventPublisherService>();
                var context = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                await using var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_try_advisory_lock(12346)";
                var acquired = (bool)(await lockCmd.ExecuteScalarAsync(stoppingToken) ?? false);

                if (acquired)
                {
                    try
                    {
                        var expired = await inventoryRepository
                            .FindExpiredReservationsAsync(ReservationTimeout, stoppingToken);

                        foreach (var inventory in expired)
                        {
                            var releasedQty = inventory.ReservedQuantity;
                            inventory.Release(releasedQty);

                            await eventPublisher.PublishInventoryEventAsync(
                                "InventoryReleased", inventory.ProductId,
                                new InventoryReleasedEvent(
                                    "TIMEOUT", inventory.ProductId, releasedQty,
                                    string.Empty, "RESERVATION_TIMEOUT",
                                    DateTimeOffset.UtcNow), stoppingToken);

                            logger.LogWarning(
                                "在庫予約タイムアウト解放: ProductId={ProductId}, Quantity={Qty}",
                                inventory.ProductId, releasedQty);
                        }

                        if (expired.Count > 0)
                            await inventoryRepository.SaveChangesAsync(stoppingToken);
                    }
                    finally
                    {
                        // H-16: finally 内は CancellationToken.None でクリーンアップを確実に実行
                        await using var unlockCmd = connection.CreateCommand();
                        unlockCmd.CommandText = "SELECT pg_advisory_unlock(12346)";
                        await unlockCmd.ExecuteScalarAsync(CancellationToken.None);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "予約クリーンアップエラー: {Message}", ex.Message);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
