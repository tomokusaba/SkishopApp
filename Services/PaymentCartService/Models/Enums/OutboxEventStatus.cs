namespace PaymentCartService.Models.Enums;

public enum OutboxEventStatus
{
    Pending,
    Processing,
    Published,
    Failed,
    DeadLetter
}
