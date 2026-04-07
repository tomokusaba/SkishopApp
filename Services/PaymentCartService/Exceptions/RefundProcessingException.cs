namespace PaymentCartService.Exceptions;

public class RefundProcessingException : Exception
{
    public string? ErrorCode { get; }
    public RefundProcessingException(string message, string? errorCode = "PAY-4222") : base(message)
        => ErrorCode = errorCode;
    public RefundProcessingException(string message, Exception innerException, string? errorCode = "PAY-4222")
        : base(message, innerException) => ErrorCode = errorCode;
}
