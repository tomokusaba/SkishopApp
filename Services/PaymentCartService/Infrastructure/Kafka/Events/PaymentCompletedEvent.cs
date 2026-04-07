namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentCompletedEvent(
    string PaymentId,
    string OrderId,
    string CustomerId,
    decimal Amount,
    string CurrencyCode,
    DateTime PaidAt);
