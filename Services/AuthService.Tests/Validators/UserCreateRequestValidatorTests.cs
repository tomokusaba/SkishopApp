using AuthService.DTOs.Requests;
using AuthService.Validators;
using FluentValidation.TestHelper;
using Shouldly;

namespace AuthService.Tests.Validators;

public class UserCreateRequestValidatorTests
{
    private readonly UserCreateRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_UsernameIsEmpty()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", string.Empty, "StrongP@ss1", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordTooShort()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "validuser", "Sh1!", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "validuser", "StrongP@ss1", "First", "Last");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_EmailIsEmpty()
    {
        // Arrange
        var request = new UserCreateRequest(string.Empty, "validuser", "StrongP@ss1", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordMissingUppercase()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "validuser", "lowercase1!", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordMissingSpecialChar()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "validuser", "StrongPass1", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_UsernameContainsSpecialChars()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "user@name!", "StrongP@ss1", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_UsernameTooShort()
    {
        // Arrange
        var request = new UserCreateRequest("valid@example.com", "ab", "StrongP@ss1", null, null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }
}
