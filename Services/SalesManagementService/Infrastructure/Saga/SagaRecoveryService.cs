using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesManagementService.Configurations;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Infrastructure.Saga;

public class SagaRecoveryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<SagaSettings> sagaOptions,
    ILogger<SagaRecoveryService> logger) : BackgroundService
{
    private readonly SagaSettings _settings = sagaOptions.Value;
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SagaRecoveryService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStalledSagasAsync(stoppingToken);
                await RecoverPendingPaymentSagasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SagaRecoveryService ポーリングエラー: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.RecoveryPollingIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("SagaRecoveryService 停止");
    }

    private async Task RecoverStalledSagasAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sagaLogRepository = scope.ServiceProvider.GetRequiredService<ISagaLogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        var threshold = timeProvider.GetUtcNow().AddMinutes(-_settings.StallThresholdMinutes);

        var stalledSagas = await dbContext.SagaLogs
            .FromSqlInterpolated(
                $"""
                SELECT * FROM saga_logs
                WHERE status = 'PROCESSING'
                  AND updated_at < {threshold}
                ORDER BY updated_at ASC
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken);

        if (stalledSagas.Count == 0)
            return;

        logger.LogWarning("滞留 Saga 検出: Count={Count}", stalledSagas.Count);

        foreach (var saga in stalledSagas)
        {
            saga.Status = "COMPENSATING";
            saga.LastError = $"滞留検出による補償開始（{_settings.StallThresholdMinutes}分超過）";
            logger.LogWarning("滞留 Saga を COMPENSATING に変更: SagaId={SagaId}, OrderId={OrderId}",
                saga.Id, saga.OrderId);
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        foreach (var saga in stalledSagas)
        {
            try
            {
                var sagaCoordinator = scope.ServiceProvider.GetRequiredService<ISagaCoordinator>();
                await sagaCoordinator.CompensateAsync(saga, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "滞留 Saga 補償失敗: SagaId={SagaId}", saga.Id);
                saga.Status = "FAILED";
                saga.LastError = $"補償失敗: {(ex.Message.Length > 200 ? ex.Message[..200] : ex.Message)}";
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }

    private async Task RecoverPendingPaymentSagasAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        var threshold = timeProvider.GetUtcNow().AddMinutes(-_settings.PaymentPollingTimeoutMinutes);

        var pendingPaymentSagas = await dbContext.SagaLogs
            .FromSqlInterpolated(
                $"""
                SELECT * FROM saga_logs
                WHERE status = 'PENDING_PAYMENT'
                  AND updated_at < {threshold}
                ORDER BY updated_at ASC
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken);

        if (pendingPaymentSagas.Count == 0)
            return;

        logger.LogWarning("決済待ちタイムアウト Saga 検出: Count={Count}", pendingPaymentSagas.Count);

        foreach (var saga in pendingPaymentSagas)
        {
            saga.Status = "COMPENSATING";
            saga.LastError = $"決済待ちタイムアウト（{_settings.PaymentPollingTimeoutMinutes}分超過）";
            logger.LogWarning(
                "決済待ち Saga を COMPENSATING に変更: SagaId={SagaId}, OrderId={OrderId}",
                saga.Id, saga.OrderId);
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        foreach (var saga in pendingPaymentSagas)
        {
            try
            {
                var sagaCoordinator = scope.ServiceProvider.GetRequiredService<ISagaCoordinator>();
                await sagaCoordinator.CompensateAsync(saga, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "決済待ち Saga 補償失敗: SagaId={SagaId}", saga.Id);
                saga.Status = "FAILED";
                saga.LastError = $"補償失敗: {(ex.Message.Length > 200 ? ex.Message[..200] : ex.Message)}";
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
