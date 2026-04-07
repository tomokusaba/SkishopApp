using Bunit;
using Frontend.Components.Shared;
using Frontend.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Components.Shared;

[Trait("Category", "Unit")]
public class PaginationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;

    public PaginationTests()
    {
        _ctx = new BunitContext();
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync()
    {
        // MudSelect requires MudPopoverProvider to be rendered
        _ctx.Render<MudPopoverProvider>();
        return Task.CompletedTask;
    }

    [Fact]
    public void Should_RenderPageNumbers_When_MultiplePages()
    {
        // Arrange
        var data = new PaginatedResult<string>(
            Items: ["a", "b"],
            TotalElements: 50,
            Page: 0,
            Size: 10);

        // Act
        var cut = _ctx.Render<PaginationComponent<string>>(parameters =>
            parameters.Add(p => p.Data, data));

        // Assert
        var markup = cut.Markup;
        markup.ShouldContain("50");
        markup.ShouldContain("1");
        markup.ShouldContain("2");
    }

    [Fact]
    public void Should_DisablePrevious_When_OnFirstPage()
    {
        // Arrange
        var data = new PaginatedResult<string>(
            Items: ["a"],
            TotalElements: 30,
            Page: 0,
            Size: 10);

        // Act
        var cut = _ctx.Render<PaginationComponent<string>>(parameters =>
            parameters.Add(p => p.Data, data));

        // Assert
        var prevButton = cut.Find("button[aria-label='前のページ']");
        prevButton.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Should_DisableNext_When_OnLastPage()
    {
        // Arrange
        var data = new PaginatedResult<string>(
            Items: ["a"],
            TotalElements: 30,
            Page: 2,
            Size: 10);

        // Act
        var cut = _ctx.Render<PaginationComponent<string>>(parameters =>
            parameters.Add(p => p.Data, data));

        // Assert
        var nextButton = cut.Find("button[aria-label='次のページ']");
        nextButton.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Should_InvokeCallback_When_PageClicked()
    {
        // Arrange
        var data = new PaginatedResult<string>(
            Items: ["a"],
            TotalElements: 30,
            Page: 0,
            Size: 10);
        int? receivedPage = null;

        var cut = _ctx.Render<PaginationComponent<string>>(parameters =>
            parameters
                .Add(p => p.Data, data)
                .Add(p => p.OnPageChanged, EventCallback.Factory.Create<int>(this, p => receivedPage = p)));

        // Act
        var nextButton = cut.Find("button[aria-label='次のページ']");
        nextButton.Click();

        // Assert
        receivedPage.ShouldBe(1);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
