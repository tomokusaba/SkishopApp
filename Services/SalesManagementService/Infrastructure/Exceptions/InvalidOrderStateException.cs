namespace SalesManagementService.Infrastructure.Exceptions;

public class InvalidOrderStateException : BusinessException
{
    public InvalidOrderStateException(string message) : base(message) { }
    public InvalidOrderStateException(string message, Exception innerException) : base(message, innerException) { }
}
