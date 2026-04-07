using System.Diagnostics.Metrics;
using SalesManagementService.Infrastructure.Persistence;

namespace SalesManagementService.Infrastructure.Telemetry;

public class SalesObservableMetrics : IDisposable
{
    private readonly Meter _meter;

    public SalesObservableMetrics(IMeterFactory meterFactory, IServiceProvider serviceProvider)
    {
        _meter = meterFactory.Create(SalesMetrics.MeterName + ".Observable", "1.0.0");

        _meter.CreateObservableGauge("saga_pending_payment_count", () =>
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            return context.SagaLogs.Count(s => s.Status == "PENDING_PAYMENT");
        }, description: "PENDING_PAYMENT状態のSaga数");

        _meter.CreateObservableGauge("outbox_pending_count", () =>
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            return context.OutboxEvents.Count(e => e.Status == "PENDING");
        }, description: "PENDING状態のOutboxイベント数");
    }

    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}
