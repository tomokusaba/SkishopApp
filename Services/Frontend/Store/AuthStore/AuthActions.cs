namespace Frontend.Store.AuthStore;

/// <summary>
/// 認証操作のアクション群
/// §6 準拠
/// </summary>
public record LoginAction(string Email, string Password);
public record LoginSuccessAction(string UserId, string UserName, string Email, string Role);
public record LoginFailureAction(string ErrorMessage);
public record LogoutAction;
public record LogoutSuccessAction;
public record CheckAuthStateAction;
public record AuthStateCheckedAction(bool IsAuthenticated, string? UserId = null, string? UserName = null, string? Email = null, string? Role = null);
