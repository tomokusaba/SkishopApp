namespace PaymentCartService.Exceptions;

public class PaymentProcessingException : Exception
{
    public string? ErrorCode { get; }
    public PaymentProcessingException(string message, string? errorCode = "PAY-4223") : base(message)
        => ErrorCode = errorCode;
    public PaymentProcessingException(string message, Exception innerException, string? errorCode = "PAY-4223")
        : base(message, innerException) => ErrorCode = errorCode;
}
