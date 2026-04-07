using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services;
using PaymentCartService.Services.Interfaces;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Services;

public class CartServiceTests
{
    private readonly ICartRepository _cartRepository;
    private readonly ICartCacheService _cacheService;
    private readonly IOptions<CartSettings> _cartOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CartService> _logger;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        _cartRepository = Substitute.For<ICartRepository>();
        _cacheService = Substitute.For<ICartCacheService>();
        _cartOptions = Options.Create(new CartSettings
        {
            ExpiryDays = 7,
            MaxItemsPerCart = 50,
            MaxQuantityPerItem = 10
        });
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<CartService>>();
        _cartService = new CartService(_cartRepository, _cacheService, _cartOptions, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCart_When_CartExists()
    {
        // Arrange
        var cart = new Cart
        {
            Id = "cart-1",
            SessionId = "session-1",
            Status = CartStatus.Active
        };
        cart.AddItem("prod-1", "スキーブーツ", "SKI-001", 25000m, 1);
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act
        var result = await _cartService.GetCartAsync("cart-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("cart-1");
        result.Items.Count.ShouldBe(1);
        result.TotalAmount.ShouldBe(25000m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_CartDoesNotExist()
    {
        // Arrange
        _cartRepository.FindByIdWithItemsAsync("nonexistent", default).Returns((Cart?)null);

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("nonexistent");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("カートが見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CartIsExpired()
    {
        // Arrange
        var cart = new Cart { Id = "cart-1", Status = CartStatus.Expired };
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("cart-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("有効ではありません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CartItemLimitExceeded()
    {
        // Arrange
        var cart = new Cart
        {
            Id = "cart-1",
            Status = CartStatus.Active
        };
        for (var i = 1; i <= 50; i++)
        {
            cart.AddItem($"prod-{i}", $"商品{i}", $"SKU-{i}", 1000m, 1);
        }
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act & Assert
        var act = async () => await _cartService.AddItemAsync(
            "cart-1", new AddCartItemRequest("prod-new", "新商品", "SKU-NEW", 1000m, 1));
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("上限");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MergeGuestCartIntoUserCart_When_UserCartExists()
    {
        // Arrange
        var guestCart = new Cart
        {
            Id = "guest-cart",
            Status = CartStatus.Active
        };
        guestCart.AddItem("prod-A", "商品A", "SKU-A", 1000m, 1);

        var userCart = new Cart
        {
            Id = "user-cart",
            CustomerId = "user-1",
            Status = CartStatus.Active
        };
        userCart.AddItem("prod-B", "商品B", "SKU-B", 2000m, 1);

        _cartRepository.FindByIdWithItemsAsync("guest-cart", default).Returns(guestCart);
        _cartRepository.FindActiveByCustomerIdAsync("user-1", default).Returns(userCart);

        // Act
        var result = await _cartService.MergeCartAsync("guest-cart", "user-1");

        // Assert
        result.Items.Count.ShouldBe(2);
        guestCart.Status.ShouldBe(CartStatus.Abandoned);
        await _cartRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _cartRepository.FindByIdWithItemsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("cart-1", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
