using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class ReturnServiceTests
{
    private readonly IReturnRepository _returnRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly OrderNumberGenerator _orderNumberGenerator;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<ReturnService> _logger;
    private readonly ReturnService _sut;

    public ReturnServiceTests()
    {
        _returnRepository = Substitute.For<IReturnRepository>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _orderNumberGenerator = new OrderNumberGenerator(_timeProvider);
        _logger = Substitute.For<ILogger<ReturnService>>();
        _sut = new ReturnService(
            _returnRepository, _orderRepository,
            _orderNumberGenerator, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateReturn_When_OrderIsDelivered()
    {
        // Arrange
        var order = CreateTestOrderWithItem("order-1", "cust-1", "DELIVERED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var request = new ReturnCreateRequest(
            "order-1", order.Items.First().Id, "DEFECTIVE", "商品に傷がありました", 1);

        // Act
        var result = await _sut.CreateReturnAsync(request, "cust-1");

        // Assert
        result.ShouldNotBeNull();
        result.OrderId.ShouldBe("order-1");
        result.Reason.ShouldBe("DEFECTIVE");
        result.Quantity.ShouldBe(1);
        result.RefundAmount.ShouldBe(10000m);
        result.Status.ShouldBe("REQUESTED");
        result.ReturnNumber.ShouldStartWith("RTN-");
        await _returnRepository.Received(1).AddAsync(Arg.Any<Return>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_OrderNotFound()
    {
        // Arrange
        _orderRepository.FindByIdWithDetailsAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        var request = new ReturnCreateRequest("nonexistent", "item-1", "DEFECTIVE");

        // Act
        var act = async () => await _sut.CreateReturnAsync(request, "cust-1");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowForbiddenException_When_OrderBelongsToOtherCustomer()
    {
        // Arrange
        var order = CreateTestOrderWithItem("order-1", "cust-1", "DELIVERED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var request = new ReturnCreateRequest(
            "order-1", order.Items.First().Id, "DEFECTIVE");

        // Act
        var act = async () => await _sut.CreateReturnAsync(request, "other-customer");

        // Assert
        await Should.ThrowAsync<ForbiddenException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_OrderItemNotFound()
    {
        // Arrange
        var order = CreateTestOrderWithItem("order-1", "cust-1", "DELIVERED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var request = new ReturnCreateRequest("order-1", "nonexistent-item", "DEFECTIVE");

        // Act
        var act = async () => await _sut.CreateReturnAsync(request, "cust-1");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent-item");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_QuantityExceedsOrderQuantity()
    {
        // Arrange
        var order = CreateTestOrderWithItem("order-1", "cust-1", "DELIVERED");
        var itemId = order.Items.First().Id;
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var request = new ReturnCreateRequest("order-1", itemId, "DEFECTIVE", Quantity: 5);

        // Act
        var act = async () => await _sut.CreateReturnAsync(request, "cust-1");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("返品数量");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnDto_When_ReturnExists()
    {
        // Arrange
        var returnEntity = CreateTestReturn("ret-1", "order-1", "cust-1");
        _returnRepository.FindByIdAsync("ret-1", Arg.Any<CancellationToken>())
            .Returns(returnEntity);

        // Act
        var result = await _sut.GetByIdAsync("ret-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("ret-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_ReturnNotFound()
    {
        // Arrange
        _returnRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Return?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    private static Order CreateTestOrderWithItem(string orderId, string customerId, string status)
    {
        var order = new Order
        {
            Id = orderId,
            OrderNumber = $"ORD-TEST-{orderId}",
            CustomerId = customerId,
            Status = status,
            PaymentStatus = "CAPTURED",
            PaymentMethod = "CREDIT_CARD",
            SubtotalAmount = 10000m,
            TaxAmount = 1000m,
            TotalAmount = 11000m,
            CurrencyCode = "JPY"
        };
        order.Items.Add(new OrderItem
        {
            Id = $"item-{orderId}",
            OrderId = orderId,
            ProductId = "prod-1",
            ProductName = "テストスキー板",
            Sku = "SKI-001",
            UnitPrice = 10000m,
            Quantity = 2,
            Subtotal = 20000m
        });
        return order;
    }

    private static Return CreateTestReturn(string returnId, string orderId, string customerId)
    {
        return new Return
        {
            Id = returnId,
            ReturnNumber = "RTN-20260318-00001",
            OrderId = orderId,
            OrderItemId = "item-1",
            CustomerId = customerId,
            Reason = "DEFECTIVE",
            Quantity = 1,
            RefundAmount = 10000m,
            Status = "REQUESTED",
            RequestedAt = DateTimeOffset.UtcNow
        };
    }
}
