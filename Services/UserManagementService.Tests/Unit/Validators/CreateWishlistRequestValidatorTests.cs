using Shouldly;
using UserManagementService.DTOs.Requests;
using UserManagementService.Validators;

namespace UserManagementService.Tests.Unit.Validators;

public class CreateWishlistRequestValidatorTests
{
    private readonly CreateWishlistRequestValidator _sut = new();

    #region Valid Requests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_NameIsValid()
    {
        // Arrange
        var request = new CreateWishlistRequest("My Wishlist");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_NameIsExactly100Chars()
    {
        // Arrange
        var request = new CreateWishlistRequest(new string('A', 100));

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_IsDefaultIsTrue()
    {
        // Arrange
        var request = new CreateWishlistRequest("お気に入り", true);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_NameIsSingleChar()
    {
        // Arrange
        var request = new CreateWishlistRequest("A");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Name Validation — Empty / Null

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_NameIsEmpty()
    {
        // Arrange
        var request = new CreateWishlistRequest("");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_NameIsWhitespaceOnly()
    {
        // Arrange
        var request = new CreateWishlistRequest("   ");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    #endregion

    #region Name Validation — Max Length

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_NameExceeds100Chars()
    {
        // Arrange
        var request = new CreateWishlistRequest(new string('A', 101));

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_NameIsVeryLong()
    {
        // Arrange
        var request = new CreateWishlistRequest(new string('あ', 200));

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    #endregion
}
