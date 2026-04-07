namespace PaymentCartService.Exceptions;

public class StripeApiException : Exception
{
    public string? ErrorCode { get; }
    public StripeApiException(string message, string? errorCode = "PAY-5002") : base(message)
        => ErrorCode = errorCode;
    public StripeApiException(string message, Exception innerException, string? errorCode = "PAY-5002")
        : base(message, innerException) => ErrorCode = errorCode;
}
