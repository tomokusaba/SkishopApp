using Frontend.DTOs;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class CartApiClientTests
{
    private readonly IApiGatewayClient _apiClient;
    private readonly ILogger<CartApiClient> _logger;
    private readonly CartApiClient _cartClient;

    public CartApiClientTests()
    {
        _apiClient = Substitute.For<IApiGatewayClient>();
        _logger = Substitute.For<ILogger<CartApiClient>>();
        _cartClient = new CartApiClient(_apiClient, _logger);
    }

    [Fact]
    public async Task Should_ReturnCart_When_GetCartCalled()
    {
        // Arrange
        var cart = new CartDto(
            Id: "cart-1",
            Items:
            [
                new CartItemDto("item-1", "prod-1", "スキーブーツ", null, "26cm", "黒", 1, 29800m, 29800m, 10)
            ],
            Subtotal: 29800m,
            Tax: 2980m,
            ShippingFee: 500m,
            CouponDiscount: 0m,
            PointDiscount: 0m,
            Total: 33280m,
            EarnablePoints: 298,
            AppliedCouponCode: null);

        _apiClient.GetAsync<CartDto>("/api/v1/cart", Arg.Any<CancellationToken>())
            .Returns(cart);

        // Act
        var result = await _cartClient.GetCartAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("cart-1");
        result.Items.Count.ShouldBe(1);
        result.Total.ShouldBe(33280m);
    }

    [Fact]
    public async Task Should_AddItem_When_AddToCartCalled()
    {
        // Arrange
        var request = new AddCartItemRequest("prod-1", 2, "26cm", "黒");

        _apiClient.PostAsync<AddCartItemRequest, object>(
            "/api/v1/cart/items",
            Arg.Any<AddCartItemRequest>(),
            Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await _cartClient.AddItemAsync(request);

        // Assert
        await _apiClient.Received(1).PostAsync<AddCartItemRequest, object>(
            "/api/v1/cart/items",
            Arg.Is<AddCartItemRequest>(r => r.ProductId == "prod-1" && r.Quantity == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RemoveItem_When_RemoveFromCartCalled()
    {
        // Arrange
        _apiClient.DeleteAsync(
            "/api/v1/cart/items/item-1",
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _cartClient.RemoveItemAsync("item-1");

        // Assert
        await _apiClient.Received(1).DeleteAsync(
            "/api/v1/cart/items/item-1",
            Arg.Any<CancellationToken>());
    }
}
