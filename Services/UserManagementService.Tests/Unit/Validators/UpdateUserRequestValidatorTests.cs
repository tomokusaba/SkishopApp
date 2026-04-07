using NSubstitute;
using Shouldly;
using UserManagementService.DTOs.Requests;
using UserManagementService.Validators;

namespace UserManagementService.Tests.Unit.Validators;

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _sut;

    public UpdateUserRequestValidatorTests()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _sut = new UpdateUserRequestValidator(timeProvider);
    }

    #region Valid Requests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_AllFieldsNull()
    {
        // Arrange
        var request = new UpdateUserRequest(null, null, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_AllFieldsValid()
    {
        // Arrange
        var request = new UpdateUserRequest("太郎", "テスト", "090-1234-5678", new DateOnly(1990, 1, 1));

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_FirstNameIsExactly100Chars()
    {
        // Arrange
        var name = new string('あ', 100);
        var request = new UpdateUserRequest(name, null, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_PhoneNumberHasValidFormats()
    {
        // Arrange
        var request = new UpdateUserRequest(null, null, "03-1234-5678", null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("090-1234-5678")]
    [InlineData("03(1234)5678")]
    [InlineData("+81-90-1234-5678")]
    [InlineData("09012345678")]
    public async Task Should_PassValidation_When_PhoneNumberFormatIsValid(string phoneNumber)
    {
        // Arrange
        var request = new UpdateUserRequest(null, null, phoneNumber, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region FirstName Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_FirstNameExceeds100Chars()
    {
        // Arrange
        var name = new string('あ', 101);
        var request = new UpdateUserRequest(name, null, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FirstName");
    }

    #endregion

    #region LastName Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_LastNameExceeds100Chars()
    {
        // Arrange
        var name = new string('テ', 101);
        var request = new UpdateUserRequest(null, name, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "LastName");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_LastNameIsExactly100Chars()
    {
        // Arrange
        var name = new string('テ', 100);
        var request = new UpdateUserRequest(null, name, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region PhoneNumber Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PhoneNumberContainsLetters()
    {
        // Arrange
        var request = new UpdateUserRequest(null, null, "abc-defg-hijk", null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PhoneNumber");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PhoneNumberExceeds20Chars()
    {
        // Arrange
        var phone = new string('1', 21);
        var request = new UpdateUserRequest(null, null, phone, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_PhoneNumberIsExactly20Chars()
    {
        // Arrange
        var phone = new string('1', 20);
        var request = new UpdateUserRequest(null, null, phone, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region BirthDate Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_BirthDateIsInFuture()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new UpdateUserRequest(null, null, null, futureDate);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "BirthDate");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_BirthDateIsInPast()
    {
        // Arrange
        var pastDate = new DateOnly(2000, 6, 15);
        var request = new UpdateUserRequest(null, null, null, pastDate);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Multiple Validation Errors

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReportMultipleErrors_When_MultipleFieldsInvalid()
    {
        // Arrange
        var request = new UpdateUserRequest(
            new string('あ', 101),
            new string('テ', 101),
            "invalid-phone!@#",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    #endregion
}
