namespace SalesManagementService.Infrastructure.Exceptions;

public class PaymentProcessingException : BusinessException
{
    public PaymentProcessingException(string message) : base(message) { }
    public PaymentProcessingException(string message, Exception innerException) : base(message, innerException) { }
}
