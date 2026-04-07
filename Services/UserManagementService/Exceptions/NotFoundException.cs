namespace UserManagementService.Exceptions;

/// <summary>
/// リソース未検出例外。HTTP 404 Not Found にマッピングされる。
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
