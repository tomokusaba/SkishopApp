using Fluxor;

namespace Frontend.Store.AuthStore;

/// <summary>
/// 認証状態の Fluxor State
/// §6 準拠 — CascadingAuthenticationState と連携
/// </summary>
[FeatureState]
public record AuthState
{
    public bool IsAuthenticated { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
