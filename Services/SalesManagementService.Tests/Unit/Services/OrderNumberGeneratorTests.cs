using Microsoft.Extensions.Time.Testing;
using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class OrderNumberGeneratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateOrderNumber_When_Called()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        var generator = new OrderNumberGenerator(fakeTime);

        // Act
        var orderNumber = generator.GenerateOrderNumber();

        // Assert
        orderNumber.ShouldStartWith("ORD-20260318-");
        orderNumber.Length.ShouldBe("ORD-yyyyMMdd-NNNNN".Length);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateReturnNumber_When_Called()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        var generator = new OrderNumberGenerator(fakeTime);

        // Act
        var returnNumber = generator.GenerateReturnNumber();

        // Assert
        returnNumber.ShouldStartWith("RTN-20260318-");
        returnNumber.Length.ShouldBe("RTN-yyyyMMdd-NNNNN".Length);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateSequentialNumbers_When_CalledMultipleTimes()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var generator = new OrderNumberGenerator(fakeTime);

        // Act
        var first = generator.GenerateOrderNumber();
        var second = generator.GenerateOrderNumber();

        // Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_IncludeDateInFormat_When_DifferentDate()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero));
        var generator = new OrderNumberGenerator(fakeTime);

        // Act
        var orderNumber = generator.GenerateOrderNumber();

        // Assert
        orderNumber.ShouldContain("20251231");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_PadSequenceWithZeros_When_Generated()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
        var generator = new OrderNumberGenerator(fakeTime);

        // Act
        var orderNumber = generator.GenerateOrderNumber();

        // Assert — sequence part should be 5 digits (zero-padded)
        var parts = orderNumber.Split('-');
        parts.Length.ShouldBe(3);
        parts[0].ShouldBe("ORD");
        parts[2].Length.ShouldBe(5);
    }
}
