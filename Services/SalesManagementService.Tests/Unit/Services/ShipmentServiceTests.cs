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

public class ShipmentServiceTests
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<ShipmentService> _logger;
    private readonly ShipmentService _sut;

    public ShipmentServiceTests()
    {
        _shipmentRepository = Substitute.For<IShipmentRepository>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<ShipmentService>>();
        _sut = new ShipmentService(_shipmentRepository, _orderRepository, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateShipment_When_OrderExists()
    {
        // Arrange
        var order = new Order
        {
            Id = "order-1",
            Status = "PROCESSING",
            ShippingRecipientName = "テスト太郎",
            ShippingPostalCode = "100-0001",
            ShippingPrefecture = "東京都",
            ShippingCity = "千代田区",
            ShippingAddressLine1 = "テスト番地1-1",
            ShippingPhoneNumber = "03-1234-5678"
        };
        _orderRepository.FindByIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var request = new ShipmentCreateRequest("order-1", "ヤマト運輸", "TRACK-001");

        // Act
        var result = await _sut.CreateShipmentAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.OrderId.ShouldBe("order-1");
        result.Carrier.ShouldBe("ヤマト運輸");
        result.TrackingNumber.ShouldBe("TRACK-001");
        result.Status.ShouldBe("PREPARING");
        await _shipmentRepository.Received(1).AddAsync(Arg.Any<Shipment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_OrderNotFoundForShipment()
    {
        // Arrange
        _orderRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        var request = new ShipmentCreateRequest("nonexistent", "ヤマト運輸");

        // Act
        var act = async () => await _sut.CreateShipmentAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnShipment_When_ValidIdProvided()
    {
        // Arrange
        var shipment = CreateTestShipment("ship-1", "order-1");
        _shipmentRepository.FindByIdAsync("ship-1", Arg.Any<CancellationToken>())
            .Returns(shipment);

        // Act
        var result = await _sut.GetByIdAsync("ship-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("ship-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_ShipmentNotFound()
    {
        // Arrange
        _shipmentRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Shipment?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnShipment_When_ValidOrderIdProvided()
    {
        // Arrange
        var shipment = CreateTestShipment("ship-1", "order-1");
        _shipmentRepository.FindByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(shipment);

        // Act
        var result = await _sut.GetByOrderIdAsync("order-1");

        // Assert
        result.ShouldNotBeNull();
        result.OrderId.ShouldBe("order-1");
    }

    private static Shipment CreateTestShipment(string id, string orderId)
    {
        return new Shipment
        {
            Id = id,
            OrderId = orderId,
            Carrier = "ヤマト運輸",
            TrackingNumber = "TRACK-001",
            Status = "PREPARING"
        };
    }
}
