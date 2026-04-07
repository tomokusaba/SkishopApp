namespace SalesManagementService.Infrastructure.Exceptions;

public class IdempotencyConflictException : BusinessException
{
    public IdempotencyConflictException(string message) : base(message) { }
    public IdempotencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
