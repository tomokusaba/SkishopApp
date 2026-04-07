namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentRefundedEvent(
    string PaymentId,
    string OrderId,
    decimal RefundAmount,
    string Reason,
    DateTime RefundedAt);
