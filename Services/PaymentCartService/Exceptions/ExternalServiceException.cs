namespace PaymentCartService.Exceptions;

public class ExternalServiceException : Exception
{
    public string? ErrorCode { get; }
    public string? ServiceName { get; }

    public ExternalServiceException(string message, string? errorCode = "PAY-5001", string? serviceName = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ServiceName = serviceName;
    }

    public ExternalServiceException(string message, Exception innerException, string? errorCode = "PAY-5001", string? serviceName = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ServiceName = serviceName;
    }
}
