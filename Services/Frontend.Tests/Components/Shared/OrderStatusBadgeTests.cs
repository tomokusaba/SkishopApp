using Bunit;
using Frontend.Components.Shared;
using MudBlazor.Services;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Components.Shared;

[Trait("Category", "Unit")]
public class OrderStatusBadgeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;

    public OrderStatusBadgeTests()
    {
        _ctx = new BunitContext();
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Should_ShowPendingBadge_When_StatusIsPending()
    {
        // Arrange & Act
        var cut = _ctx.Render<OrderStatusBadge>(parameters =>
            parameters.Add(p => p.Status, "PENDING"));

        // Assert
        cut.Markup.ShouldContain("注文受付");
    }

    [Fact]
    public void Should_ShowShippedBadge_When_StatusIsShipped()
    {
        // Arrange & Act
        var cut = _ctx.Render<OrderStatusBadge>(parameters =>
            parameters.Add(p => p.Status, "SHIPPED"));

        // Assert
        cut.Markup.ShouldContain("発送済み");
    }

    [Fact]
    public void Should_ShowDeliveredBadge_When_StatusIsDelivered()
    {
        // Arrange & Act
        var cut = _ctx.Render<OrderStatusBadge>(parameters =>
            parameters.Add(p => p.Status, "DELIVERED"));

        // Assert
        cut.Markup.ShouldContain("配達完了");
    }

    [Fact]
    public void Should_ShowCancelledBadge_When_StatusIsCancelled()
    {
        // Arrange & Act
        var cut = _ctx.Render<OrderStatusBadge>(parameters =>
            parameters.Add(p => p.Status, "CANCELLED"));

        // Assert
        cut.Markup.ShouldContain("キャンセル");
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
