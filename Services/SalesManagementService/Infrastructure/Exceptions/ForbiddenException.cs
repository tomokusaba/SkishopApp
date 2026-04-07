namespace SalesManagementService.Infrastructure.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("アクセスが拒否されました") { }
    public ForbiddenException(string message) : base(message) { }
    public ForbiddenException(string message, Exception innerException) : base(message, innerException) { }
}
