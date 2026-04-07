using Bunit;
using Frontend.Components.Shared;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Components.Shared;

[Trait("Category", "Unit")]
public class StockBadgeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;

    public StockBadgeTests()
    {
        _ctx = new BunitContext();
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Should_ShowInStock_When_QuantityAboveThreshold()
    {
        // Arrange & Act
        var cut = _ctx.Render<StockBadge>(parameters =>
            parameters.Add(p => p.StockQuantity, 20));

        // Assert
        var markup = cut.Markup;
        markup.ShouldContain("在庫あり");
    }

    [Fact]
    public void Should_ShowLowStock_When_QuantityBelowThreshold()
    {
        // Arrange & Act
        var cut = _ctx.Render<StockBadge>(parameters =>
            parameters
                .Add(p => p.StockQuantity, 3)
                .Add(p => p.LowStockThreshold, 5));

        // Assert
        var markup = cut.Markup;
        markup.ShouldContain("残りわずか");
        markup.ShouldContain("3");
    }

    [Fact]
    public void Should_ShowOutOfStock_When_QuantityIsZero()
    {
        // Arrange & Act
        var cut = _ctx.Render<StockBadge>(parameters =>
            parameters.Add(p => p.StockQuantity, 0));

        // Assert
        var markup = cut.Markup;
        markup.ShouldContain("在庫切れ");
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
