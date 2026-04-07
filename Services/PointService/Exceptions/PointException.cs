namespace PointService.Exceptions;

/// <summary>ポイントサービス基底例外。</summary>
public class PointException : Exception
{
    public string ErrorCode { get; }

    public PointException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public PointException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
