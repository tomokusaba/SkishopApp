using Bunit;
using Frontend.Components.Shared;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Components.Shared;

[Trait("Category", "Unit")]
public class SearchSuggestTests : IAsyncLifetime
{
    private BunitContext _ctx = default!;
    private IAiApiClient _aiApiClient = default!;

    public Task InitializeAsync()
    {
        _ctx = new BunitContext();
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var apiGatewayClient = Substitute.For<IApiGatewayClient>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<AiApiClient>>();
        _aiApiClient = new AiApiClient(apiGatewayClient, logger);

        _ctx.Services.AddSingleton<IAiApiClient>(_aiApiClient);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void Should_RenderSearchInput_When_ComponentLoaded()
    {
        // Arrange & Act
        var cut = _ctx.Render<SearchSuggest>();

        // Assert
        var container = cut.Find(".search-suggest-container");
        container.ShouldNotBeNull();
        container.GetAttribute("role").ShouldBe("combobox");
    }

    [Fact]
    public void Should_HavePlaceholderText_When_Rendered()
    {
        // Arrange & Act
        var cut = _ctx.Render<SearchSuggest>();

        // Assert
        var markup = cut.Markup;
        markup.ShouldContain("商品を検索");
    }

    [Fact]
    public void Should_HaveAriaAttributes_When_Rendered()
    {
        // Arrange & Act
        var cut = _ctx.Render<SearchSuggest>();

        // Assert
        var container = cut.Find("[role='combobox']");
        container.ShouldNotBeNull();
        container.GetAttribute("aria-haspopup").ShouldBe("listbox");
    }
}
