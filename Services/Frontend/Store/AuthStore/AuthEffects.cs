using Fluxor;
using Frontend.Services;
using Frontend.Services.Interfaces;

namespace Frontend.Store.AuthStore;

/// <summary>
/// 認証の副作用処理（API 通信）
/// §6 / §16.2 準拠 — BFF パターンでの認証フロー
/// </summary>
public class AuthEffects(
    IAuthApiClient authApi,
    ITokenStorageService tokenStorage,
    ILogger<AuthEffects> logger)
{
    [EffectMethod]
    public async Task HandleLogin(LoginAction action, IDispatcher dispatcher)
    {
        try
        {
            var response = await authApi.LoginAsync(action.Email, action.Password);

            if (response.MfaRequired)
            {
                dispatcher.Dispatch(new LoginFailureAction("MFA が必要です"));
                return;
            }

            tokenStorage.StoreTokens(response.AccessToken, response.RefreshToken, response.ExpiresIn);

            dispatcher.Dispatch(new LoginSuccessAction(
                response.User.Id,
                $"{response.User.FirstName} {response.User.LastName}",
                action.Email,
                response.User.Role));

            await authApi.MergeCartAsync();
        }
        catch (UnauthorizedAccessException)
        {
            dispatcher.Dispatch(new LoginFailureAction("メールアドレスまたはパスワードが正しくありません"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ログインエラー");
            dispatcher.Dispatch(new LoginFailureAction("サーバーエラーが発生しました"));
        }
    }

    [EffectMethod]
    public async Task HandleLogout(LogoutAction _, IDispatcher dispatcher)
    {
        try
        {
            await authApi.LogoutAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ログアウトエラー");
        }
        finally
        {
            tokenStorage.ClearTokens();
            dispatcher.Dispatch(new LogoutSuccessAction());
        }
    }
}
