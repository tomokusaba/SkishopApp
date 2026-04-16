using McpServer.DTOs.Responses;
using Shouldly;
using Xunit;

namespace McpServer.Tests.DTOs.Responses;

public class PaginatedCatalogProductsResultTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_CalculatePaginationFlags_When_MultiplePagesExist()
    {
        // Arrange
        var result = new PaginatedCatalogProductsResult([], 25, 1, 10);

        // Act
        var totalPages = result.TotalPages;
        var hasNext = result.HasNext;
        var hasPrevious = result.HasPrevious;

        // Assert
        totalPages.ShouldBe(3);
        hasNext.ShouldBeTrue();
        hasPrevious.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_NotExposeNextPage_When_CurrentPageIsLastPage()
    {
        // Arrange
        var result = new PaginatedCatalogProductsResult([], 20, 1, 10);

        // Act
        var hasNext = result.HasNext;
        var hasPrevious = result.HasPrevious;

        // Assert
        hasNext.ShouldBeFalse();
        hasPrevious.ShouldBeTrue();
    }
}
