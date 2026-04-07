using System.Diagnostics.Metrics;

namespace SalesManagementService.Infrastructure.Telemetry;

public class SalesMetrics
{
    public static readonly string MeterName = "SkiShop.SalesManagement";

    private readonly Counter<long> _orderCreationCounter;
    private readonly Histogram<double> _orderProcessingHistogram;
    private readonly Counter<long> _paymentSuccessCounter;
    private readonly Counter<long> _paymentTotalCounter;
    private readonly Counter<long> _sagaCompletedCounter;
    private readonly Counter<long> _sagaTotalCounter;
    private readonly Counter<long> _grpcDeadlineExceededCounter;
    private readonly Counter<long> _grpcTotalCounter;
    private readonly Counter<long> _compensationCounter;
    private readonly Counter<long> _apiErrorCounter;
    private readonly Counter<long> _apiTotalCounter;
    private readonly Histogram<double> _outboxPublishLatencyHistogram;

    public SalesMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, "1.0.0");

        _orderCreationCounter = meter.CreateCounter<long>("order_creation_rate", "orders", "注文作成レート");
        _orderProcessingHistogram = meter.CreateHistogram<double>("order_processing_time_ms", "ms", "注文処理時間");
        _paymentSuccessCounter = meter.CreateCounter<long>("payment_success_count", "payments", "決済成功数");
        _paymentTotalCounter = meter.CreateCounter<long>("payment_total_count", "payments", "決済試行数");
        _sagaCompletedCounter = meter.CreateCounter<long>("saga_completed_count", "sagas", "Saga完了数");
        _sagaTotalCounter = meter.CreateCounter<long>("saga_total_count", "sagas", "Saga試行数");
        _grpcDeadlineExceededCounter = meter.CreateCounter<long>("grpc_deadline_exceeded_count", "calls", "gRPC Deadline超過数");
        _grpcTotalCounter = meter.CreateCounter<long>("grpc_total_count", "calls", "gRPC総呼出数");
        _compensationCounter = meter.CreateCounter<long>("compensation_execution_count", "compensations", "補償実行数");
        _apiErrorCounter = meter.CreateCounter<long>("api_error_count", "errors", "APIエラー数");
        _apiTotalCounter = meter.CreateCounter<long>("api_total_count", "requests", "APIリクエスト数");
        _outboxPublishLatencyHistogram = meter.CreateHistogram<double>("outbox_publish_latency_ms", "ms", "Outbox発行遅延");
    }

    public void RecordOrderCreated() => _orderCreationCounter.Add(1);
    public void RecordOrderProcessingTime(double ms) => _orderProcessingHistogram.Record(ms);

    public void RecordPaymentAttempt(bool success)
    {
        _paymentTotalCounter.Add(1);
        if (success) _paymentSuccessCounter.Add(1);
    }

    public void RecordSagaAttempt(bool completed)
    {
        _sagaTotalCounter.Add(1);
        if (completed) _sagaCompletedCounter.Add(1);
    }

    public void RecordGrpcCall(bool deadlineExceeded)
    {
        _grpcTotalCounter.Add(1);
        if (deadlineExceeded) _grpcDeadlineExceededCounter.Add(1);
    }

    public void RecordCompensation() => _compensationCounter.Add(1);

    public void RecordApiRequest(bool isError)
    {
        _apiTotalCounter.Add(1);
        if (isError) _apiErrorCounter.Add(1);
    }

    public void RecordOutboxPublishLatency(double ms) => _outboxPublishLatencyHistogram.Record(ms);
}
