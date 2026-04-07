using Xunit;
using AiSupportService.Infrastructure.SemanticKernel;
using Shouldly;

namespace AiSupportService.Tests.Infrastructure;

public class ResponseFilterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_MaskCreditCardNumber()
    {
        // Arrange
        var input = "カード番号は 4111-1111-1111-1111 です";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("4111");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_MaskPhoneNumber()
    {
        // Arrange
        var input = "電話番号: 09012345678";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("0901234");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_MaskEmail()
    {
        // Arrange
        var input = "メール: user@example.com にお送りします";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("user@example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnOriginal_When_NoPiiDetected()
    {
        // Arrange
        var input = "スキーブーツは初心者におすすめです";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldBe(input);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_MaskInternalPaths()
    {
        // Arrange
        var input = "ファイルは /usr/local/app/config.json にあります";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldContain("[PATH_REDACTED]");
        result.ShouldNotContain("/usr/local/app");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_MaskMultiplePiiTypes_When_ResponseContainsAll()
    {
        // Arrange
        var input = "Email: admin@example.com, Phone: 09012345678, Card: 4111-1111-1111-1111";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldNotContain("admin@example.com");
        result.ShouldNotContain("09012345678");
        result.ShouldNotContain("4111");
        result.ShouldContain("[REDACTED]");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ThrowArgumentNullException_When_ResponseIsNull()
    {
        // Act / Assert
        Should.Throw<ArgumentNullException>(() => ResponseFilter.Filter(null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_AllowShortPaths_When_NotInternalPath()
    {
        // /api/v1 is only 2 levels, should not be masked
        var input = "APIは /api/v1 で提供されています";

        // Act
        var result = ResponseFilter.Filter(input);

        // Assert
        result.ShouldNotContain("[PATH_REDACTED]");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnEmpty_When_EmptyInput()
    {
        // Act
        var result = ResponseFilter.Filter(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }
}
