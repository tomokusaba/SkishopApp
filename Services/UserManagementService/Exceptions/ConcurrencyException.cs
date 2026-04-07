namespace UserManagementService.Exceptions;

/// <summary>
/// 楽観的ロック競合例外。HTTP 409 Conflict にマッピングされる。
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> をラップする。
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
