using Frontend.Store.AuthStore;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Store;

[Trait("Category", "Unit")]
public class AuthStoreTests
{
    [Fact]
    public void Should_SetAuthenticated_When_LoginSuccessAction()
    {
        // Arrange
        var state = new AuthState { IsLoading = true };
        var action = new LoginSuccessAction("user-1", "田中太郎", "tanaka@example.com", "User");

        // Act
        var newState = AuthReducers.OnLoginSuccess(state, action);

        // Assert
        newState.IsAuthenticated.ShouldBeTrue();
        newState.UserId.ShouldBe("user-1");
        newState.UserName.ShouldBe("田中太郎");
        newState.Email.ShouldBe("tanaka@example.com");
        newState.Role.ShouldBe("User");
        newState.IsLoading.ShouldBeFalse();
        newState.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_ClearAuth_When_LogoutAction()
    {
        // Arrange
        var state = new AuthState
        {
            IsAuthenticated = true,
            UserId = "user-1",
            UserName = "田中太郎",
            Email = "tanaka@example.com",
            Role = "User"
        };

        // Act
        var logoutState = AuthReducers.OnLogout(state);
        var newState = AuthReducers.OnLogoutSuccess(logoutState);

        // Assert
        newState.IsAuthenticated.ShouldBeFalse();
        newState.UserId.ShouldBeNull();
        newState.UserName.ShouldBeNull();
        newState.Email.ShouldBeNull();
        newState.Role.ShouldBeNull();
        newState.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public void Should_SetLoading_When_LoginAction()
    {
        // Arrange
        var state = new AuthState();

        // Act
        var newState = AuthReducers.OnLogin(state);

        // Assert
        newState.IsLoading.ShouldBeTrue();
        newState.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_SetError_When_LoginFailureAction()
    {
        // Arrange
        var state = new AuthState { IsLoading = true };
        var action = new LoginFailureAction("認証情報が無効です");

        // Act
        var newState = AuthReducers.OnLoginFailure(state, action);

        // Assert
        newState.IsLoading.ShouldBeFalse();
        newState.ErrorMessage.ShouldBe("認証情報が無効です");
    }
}
