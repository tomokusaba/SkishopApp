namespace PaymentCartService.Infrastructure.Kafka.Events;

public record OrderCancelledEvent(
    string OrderId,
    string CustomerId,
    string Reason,
    DateTime OccurredAt);
