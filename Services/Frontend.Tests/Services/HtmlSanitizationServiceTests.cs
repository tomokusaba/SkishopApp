using Frontend.Services;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class HtmlSanitizationServiceTests
{
    private readonly HtmlSanitizationService _service;

    public HtmlSanitizationServiceTests()
    {
        _service = new HtmlSanitizationService();
    }

    [Fact]
    public void Should_RemoveScriptTags_When_InputContainsScript()
    {
        // Arrange
        var dirtyHtml = "<p>Hello</p><script>alert('xss')</script><p>World</p>";

        // Act
        var result = _service.Sanitize(dirtyHtml);

        // Assert
        result.Value.ShouldNotContain("<script>");
        result.Value.ShouldNotContain("alert");
        result.Value.ShouldContain("<p>Hello</p>");
        result.Value.ShouldContain("<p>World</p>");
    }

    [Fact]
    public void Should_AllowSafeHtml_When_InputIsClean()
    {
        // Arrange
        var cleanHtml = "<p>こんにちは</p><strong>太字</strong><em>斜体</em>";

        // Act
        var result = _service.Sanitize(cleanHtml);

        // Assert
        result.Value.ShouldContain("<p>こんにちは</p>");
        result.Value.ShouldContain("<strong>太字</strong>");
        result.Value.ShouldContain("<em>斜体</em>");
    }

    [Fact]
    public void Should_RemoveOnClickHandlers_When_InputContainsEventHandlers()
    {
        // Arrange
        var dirtyHtml = "<p onclick=\"alert('xss')\">Click me</p><a href=\"https://example.com\" onmouseover=\"steal()\">Link</a>";

        // Act
        var result = _service.Sanitize(dirtyHtml);

        // Assert
        result.Value.ShouldNotContain("onclick");
        result.Value.ShouldNotContain("onmouseover");
        result.Value.ShouldContain("Click me");
        result.Value.ShouldContain("https://example.com");
    }
}
