using FluentValidation.TestHelper;
using InventoryManagementService.Validators;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace InventoryManagementService.Tests.Validators;

/// <summary>
/// <see cref="ImageUploadRequestValidator"/> の単体テスト。
/// FluentValidation の TestValidateAsync を使用し、
/// ファイルサイズ・Content-Type・マジックバイトのバリデーションルールを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class ImageUploadRequestValidatorTests
{
    private readonly ImageUploadRequestValidator _validator = new();

    [Fact]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var file = CreateMockFormFile(
            contentType: "image/jpeg",
            magicBytes: [0xFF, 0xD8, 0xFF, 0xE0],
            length: 1024);

        // Act
        var result = await _validator.TestValidateAsync(file);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_FailValidation_When_FileIsEmpty()
    {
        // Arrange
        var file = CreateMockFormFile(
            contentType: "image/jpeg",
            magicBytes: [],
            length: 0);

        // Act
        var result = await _validator.TestValidateAsync(file);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Fact]
    public async Task Should_FailValidation_When_FileIsTooLarge()
    {
        // Arrange
        var maxSize = 10 * 1024 * 1024;
        var file = CreateMockFormFile(
            contentType: "image/jpeg",
            magicBytes: [0xFF, 0xD8, 0xFF, 0xE0],
            length: maxSize + 1);

        // Act
        var result = await _validator.TestValidateAsync(file);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    public async Task Should_FailValidation_When_ContentTypeIsInvalid(string contentType)
    {
        // Arrange
        var file = CreateMockFormFile(
            contentType: contentType,
            magicBytes: [0x00, 0x00, 0x00, 0x00],
            length: 1024);

        // Act
        var result = await _validator.TestValidateAsync(file);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Fact]
    public async Task Should_FailValidation_When_MagicBytesMismatch()
    {
        // Arrange — Content-Type は image/png だがバイトは JPEG
        var file = CreateMockFormFile(
            contentType: "image/png",
            magicBytes: [0xFF, 0xD8, 0xFF, 0xE0],
            length: 1024);

        // Act
        var result = await _validator.TestValidateAsync(file);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// テスト用 IFormFile モックを生成する。指定されたマジックバイトを返すストリームを構成する。
    /// </summary>
    private static IFormFile CreateMockFormFile(string contentType, byte[] magicBytes, long length)
    {
        var file = Substitute.For<IFormFile>();
        file.ContentType.Returns(contentType);
        file.Length.Returns(length);

        var stream = new MemoryStream(magicBytes.Length > 0 ? magicBytes : [0x00]);
        file.OpenReadStream().Returns(stream);

        return file;
    }
}
