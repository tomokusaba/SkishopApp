using Shouldly;
using UserManagementService.DTOs.Requests;
using UserManagementService.Models;
using UserManagementService.Validators;

namespace UserManagementService.Tests.Unit.Validators;

public class CreateAddressRequestValidatorTests
{
    private readonly CreateAddressRequestValidator _sut = new();

    private static CreateAddressRequest CreateValidRequest() => new(
        AddressType.Shipping,
        "テスト太郎",
        "123-4567",
        "東京都",
        "渋谷区",
        "道玄坂1-2-3",
        "テストビル5F",
        "03-1234-5678");

    #region Valid Requests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_AllFieldsValid()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_OptionalFieldsAreNull()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Billing, "太郎", "123-4567", "大阪府", "大阪市", "梅田1-1-1", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("SHIPPING")]
    [InlineData("BILLING")]
    public async Task Should_PassValidation_When_AddressTypeIsValid(string addressType)
    {
        // Arrange
        var request = new CreateAddressRequest(
            addressType, "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("1234567")]
    [InlineData("123-4567")]
    public async Task Should_PassValidation_When_ZipCodeFormatIsValid(string zipCode)
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", zipCode, "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region AddressType Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_AddressTypeIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            "", "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AddressType");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_AddressTypeIsInvalid()
    {
        // Arrange
        var request = new CreateAddressRequest(
            "HOME", "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AddressType");
    }

    #endregion

    #region Recipient Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_RecipientIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Recipient");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_RecipientExceeds100Chars()
    {
        // Arrange
        var longName = new string('あ', 101);
        var request = new CreateAddressRequest(
            AddressType.Shipping, longName, "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Recipient");
    }

    #endregion

    #region ZipCode (PostalCode) Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_ZipCodeIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ZipCode");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("12345")]
    [InlineData("12-34567")]
    [InlineData("abc-defg")]
    [InlineData("12345678")]
    public async Task Should_FailValidation_When_ZipCodeFormatIsInvalid(string zipCode)
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", zipCode, "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ZipCode");
    }

    #endregion

    #region Prefecture Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PrefectureIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Prefecture");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PrefectureExceeds50Chars()
    {
        // Arrange
        var longPrefecture = new string('あ', 51);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", longPrefecture, "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Prefecture");
    }

    #endregion

    #region City Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_CityIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "City");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_CityExceeds100Chars()
    {
        // Arrange
        var longCity = new string('市', 101);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", longCity, "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "City");
    }

    #endregion

    #region StreetAddress Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_StreetAddressIsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "渋谷区", "", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "StreetAddress");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_StreetAddressExceeds255Chars()
    {
        // Arrange
        var longAddress = new string('道', 256);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "渋谷区", longAddress, null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "StreetAddress");
    }

    #endregion

    #region Building Validation (optional)

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_BuildingExceeds255Chars()
    {
        // Arrange
        var longBuilding = new string('ビ', 256);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", longBuilding, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Building");
    }

    #endregion

    #region PhoneNumber Validation (optional)

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PhoneNumberContainsLetters()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, "abc-defg-hijk");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PhoneNumber");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_PhoneNumberIsNull()
    {
        // Arrange
        var request = new CreateAddressRequest(
            AddressType.Shipping, "太郎", "123-4567", "東京都", "渋谷区", "道玄坂1-2-3", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Multiple Errors

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReportMultipleErrors_When_MultipleRequiredFieldsEmpty()
    {
        // Arrange
        var request = new CreateAddressRequest("", "", "", "", "", "", null, null);

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    #endregion
}
