namespace PaymentCartService.Infrastructure.Kafka.Events;

public record UserDeletedEvent(
    string UserId,
    DateTime OccurredAt);
