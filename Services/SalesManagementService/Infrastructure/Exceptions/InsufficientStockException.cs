namespace SalesManagementService.Infrastructure.Exceptions;

public class InsufficientStockException : BusinessException
{
    public InsufficientStockException(string message) : base(message) { }
    public InsufficientStockException(string message, Exception innerException) : base(message, innerException) { }
}
