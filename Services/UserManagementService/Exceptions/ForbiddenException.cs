namespace UserManagementService.Exceptions;

/// <summary>
/// アクセス拒否例外。HTTP 403 Forbidden にマッピングされる。IDOR 防止時等に使用。
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("アクセスが拒否されました") { }
    public ForbiddenException(string message) : base(message) { }
}
