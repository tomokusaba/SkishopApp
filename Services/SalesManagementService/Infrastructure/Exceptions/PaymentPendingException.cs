namespace SalesManagementService.Infrastructure.Exceptions;

public class PaymentPendingException : BusinessException
{
    public PaymentPendingException(string message) : base(message) { }
    public PaymentPendingException(string message, Exception innerException) : base(message, innerException) { }
}
