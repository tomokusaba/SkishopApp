namespace SalesManagementService.Infrastructure.Exceptions;

public class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);
