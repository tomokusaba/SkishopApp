using System.Diagnostics.Metrics;

namespace PointService.Infrastructure.Telemetry;

public class PointServiceMetrics
{
    public const string MeterName = "PointService";

    private readonly Counter<long> _pointsEarned;
    private readonly Counter<long> _pointsRedeemed;
    private readonly Counter<long> _pointsExpired;
    private readonly Counter<long> _pointsReserved;
    private readonly Counter<long> _pointsReleased;
    private readonly Histogram<double> _operationDuration;
    private readonly Counter<long> _outboxEventsPublished;
    private readonly Counter<long> _outboxEventsFailed;

    public PointServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _pointsEarned = meter.CreateCounter<long>(
            "point_service.points.earned", "points", "Total points earned");
        _pointsRedeemed = meter.CreateCounter<long>(
            "point_service.points.redeemed", "points", "Total points redeemed");
        _pointsExpired = meter.CreateCounter<long>(
            "point_service.points.expired", "points", "Total points expired");
        _pointsReserved = meter.CreateCounter<long>(
            "point_service.points.reserved", "points", "Total points reserved");
        _pointsReleased = meter.CreateCounter<long>(
            "point_service.points.released", "points", "Total points released");
        _operationDuration = meter.CreateHistogram<double>(
            "point_service.operation.duration", "ms", "Operation duration in milliseconds");
        _outboxEventsPublished = meter.CreateCounter<long>(
            "point_service.outbox.published", description: "Outbox events published successfully");
        _outboxEventsFailed = meter.CreateCounter<long>(
            "point_service.outbox.failed", description: "Outbox events failed to publish");
    }

    public void RecordPointsEarned(long points) => _pointsEarned.Add(points);
    public void RecordPointsRedeemed(long points) => _pointsRedeemed.Add(points);
    public void RecordPointsExpired(long points) => _pointsExpired.Add(points);
    public void RecordPointsReserved(long points) => _pointsReserved.Add(points);
    public void RecordPointsReleased(long points) => _pointsReleased.Add(points);
    public void RecordOperationDuration(string operation, double durationMs)
        => _operationDuration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
    public void RecordOutboxPublished() => _outboxEventsPublished.Add(1);
    public void RecordOutboxFailed() => _outboxEventsFailed.Add(1);
}
