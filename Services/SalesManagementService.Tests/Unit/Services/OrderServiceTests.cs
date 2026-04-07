using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SalesManagementService.Configurations;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.Saga;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class OrderServiceTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly OrderNumberGenerator _orderNumberGenerator;
    private readonly ShippingFeeCalculator _shippingFeeCalculator;
    private readonly ILogger<OrderService> _logger;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        var fakeTime = TimeProvider.System;
        _orderNumberGenerator = new OrderNumberGenerator(fakeTime);
        var shippingSettings = new ShippingSettings(
            FreeShippingThreshold: 10000,
            DefaultShippingFee: 660,
            HokkaidoOkinawaFee: 1100,
            ExpressSurcharge: 330,
            LargeItemSurcharge: 1650,
            MemberRankDiscounts: new Dictionary<string, decimal>
            {
                ["Silver"] = 8000, ["Gold"] = 5000, ["Platinum"] = 0
            });
        _shippingFeeCalculator = new ShippingFeeCalculator(Options.Create(shippingSettings));
        _logger = Substitute.For<ILogger<OrderService>>();
        _sut = new OrderService(_orderRepository, _orderNumberGenerator, _shippingFeeCalculator, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnOrderDetail_When_ValidIdProvided()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "cust-1", "PENDING");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var result = await _sut.GetByIdAsync("order-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("order-1");
        result.CustomerId.ShouldBe("cust-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_OrderDoesNotExist()
    {
        // Arrange
        _orderRepository.FindByIdWithDetailsAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnOrderDetail_When_OrderBelongsToUser()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "user-1", "PENDING");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var result = await _sut.GetByIdAndUserIdAsync("order-1", "user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("order-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowForbiddenException_When_OrderBelongsToOtherUser()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "user-1", "PENDING");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var act = async () => await _sut.GetByIdAndUserIdAsync("order-1", "other-user");

        // Assert
        await Should.ThrowAsync<ForbiddenException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_OrderNotFoundForUserCheck()
    {
        // Arrange
        _orderRepository.FindByIdWithDetailsAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        // Act
        var result = await _sut.GetByIdAndUserIdAsync("nonexistent", "user-1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnOrderDetail_When_ValidOrderNumberProvided()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "cust-1", "PENDING");
        order.OrderNumber = "ORD-20260318-00001";
        _orderRepository.FindByOrderNumberAsync("ORD-20260318-00001", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var result = await _sut.GetByOrderNumberAsync("ORD-20260318-00001");

        // Assert
        result.ShouldNotBeNull();
        result.OrderNumber.ShouldBe("ORD-20260318-00001");
    }

    private static Order CreateTestOrder(string id, string customerId, string status)
    {
        return new Order
        {
            Id = id,
            OrderNumber = $"ORD-TEST-{id}",
            CustomerId = customerId,
            Status = status,
            PaymentStatus = "PENDING",
            PaymentMethod = "CREDIT_CARD",
            SubtotalAmount = 10000m,
            TaxAmount = 1000m,
            ShippingFee = 550m,
            TotalAmount = 11550m,
            CurrencyCode = "JPY",
            Items = new List<OrderItem>
            {
                new()
                {
                    Id = $"item-{id}",
                    ProductId = "prod-1",
                    ProductName = "テストスキー板",
                    Sku = "SKI-001",
                    UnitPrice = 10000m,
                    Quantity = 1,
                    Subtotal = 10000m
                }
            }
        };
    }
}
