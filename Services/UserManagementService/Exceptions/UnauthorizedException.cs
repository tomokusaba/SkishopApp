namespace UserManagementService.Exceptions;

/// <summary>
/// 認証失敗例外。HTTP 401 Unauthorized にマッピングされる。
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("認証が必要です") { }
    public UnauthorizedException(string message) : base(message) { }
}
