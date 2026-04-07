namespace PaymentCartService.Infrastructure.Kafka.Events;

public record OrderCreatedEvent(
    string OrderId,
    string CustomerId,
    decimal TotalAmount,
    string CurrencyCode,
    DateTime OccurredAt);
