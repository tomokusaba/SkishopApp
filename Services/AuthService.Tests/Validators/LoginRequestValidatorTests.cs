using AuthService.DTOs.Requests;
using AuthService.Validators;
using FluentValidation.TestHelper;
using Shouldly;

namespace AuthService.Tests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_EmailIsEmpty()
    {
        // Arrange
        var request = new LoginRequest(string.Empty, "ValidPassword1!");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_EmailIsInvalid()
    {
        // Arrange
        var request = new LoginRequest("not-an-email", "ValidPassword1!");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordIsEmpty()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", string.Empty);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordTooShort()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "Short1!");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidCredentials()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "ValidPassword1!");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_EmailExceedsMaxLength()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@b.com";
        var request = new LoginRequest(longEmail, "ValidPassword1!");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
