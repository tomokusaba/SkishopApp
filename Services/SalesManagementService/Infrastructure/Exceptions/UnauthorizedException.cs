namespace SalesManagementService.Infrastructure.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("認証が必要です") { }
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}
