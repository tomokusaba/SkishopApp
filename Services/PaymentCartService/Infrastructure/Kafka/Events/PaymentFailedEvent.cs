namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentFailedEvent(
    string PaymentId,
    string OrderId,
    string? FailureCode,
    string? FailureMessage);
