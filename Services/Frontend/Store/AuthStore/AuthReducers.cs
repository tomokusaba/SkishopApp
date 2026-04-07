using Fluxor;

namespace Frontend.Store.AuthStore;

/// <summary>
/// 認証状態のリデューサー群
/// §6 準拠
/// </summary>
public static class AuthReducers
{
    [ReducerMethod(typeof(LoginAction))]
    public static AuthState OnLogin(AuthState state)
        => state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static AuthState OnLoginSuccess(AuthState state, LoginSuccessAction action)
        => state with
        {
            IsAuthenticated = true,
            UserId = action.UserId,
            UserName = action.UserName,
            Email = action.Email,
            Role = action.Role,
            IsLoading = false,
            ErrorMessage = null
        };

    [ReducerMethod]
    public static AuthState OnLoginFailure(AuthState state, LoginFailureAction action)
        => state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    [ReducerMethod(typeof(LogoutAction))]
    public static AuthState OnLogout(AuthState state)
        => state with { IsLoading = true };

    [ReducerMethod(typeof(LogoutSuccessAction))]
    public static AuthState OnLogoutSuccess(AuthState state)
        => new AuthState();

    [ReducerMethod]
    public static AuthState OnAuthStateChecked(AuthState state, AuthStateCheckedAction action)
        => state with
        {
            IsAuthenticated = action.IsAuthenticated,
            UserId = action.UserId,
            UserName = action.UserName,
            Email = action.Email,
            Role = action.Role,
            IsLoading = false
        };
}
