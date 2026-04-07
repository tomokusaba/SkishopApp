namespace UserManagementService.Exceptions;

/// <summary>
/// ビジネスルール違反例外。HTTP 422 Unprocessable Entity にマッピングされる。
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
    public BusinessException(string message, Exception innerException) : base(message, innerException) { }
}
