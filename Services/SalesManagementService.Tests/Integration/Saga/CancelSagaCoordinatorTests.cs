using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.ExternalServices;
using SalesManagementService.Infrastructure.Saga;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;
using Shouldly;

namespace SalesManagementService.Tests.Integration.Saga;

public class CancelSagaCoordinatorTests
{
    private readonly ISagaLogRepository _sagaLogRepository;
    private readonly IOrderService _orderService;
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryClient _inventoryClient;
    private readonly ICouponClient _couponClient;
    private readonly IPointClient _pointClient;
    private readonly IPaymentClient _paymentClient;
    private readonly IOutboxWriter _outboxWriter;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<CancelSagaCoordinator> _logger;
    private readonly CancelSagaCoordinator _sut;

    public CancelSagaCoordinatorTests()
    {
        _sagaLogRepository = Substitute.For<ISagaLogRepository>();
        _orderService = Substitute.For<IOrderService>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _inventoryClient = Substitute.For<IInventoryClient>();
        _couponClient = Substitute.For<ICouponClient>();
        _pointClient = Substitute.For<IPointClient>();
        _paymentClient = Substitute.For<IPaymentClient>();
        _outboxWriter = Substitute.For<IOutboxWriter>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<CancelSagaCoordinator>>();

        _sut = new CancelSagaCoordinator(
            _sagaLogRepository, _orderService, _orderRepository,
            _inventoryClient, _couponClient, _pointClient,
            _paymentClient, _outboxWriter, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_CancelOrder_When_StatusIsPending()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "PENDING");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);
        _sagaLogRepository.FindByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns((SagaLog?)null);

        // Act
        await _sut.ExecuteCancelSagaAsync("order-1", "ユーザーリクエスト");

        // Assert
        await _orderService.Received(1).CancelOrderAsync(
            "order-1", "ユーザーリクエスト", Arg.Any<CancellationToken>());
        await _outboxWriter.Received(1).WriteAsync(
            "order.cancelled", "order-1", Arg.Any<OrderCancelledEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_CancelOrder_When_StatusIsConfirmed()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "CONFIRMED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);
        _sagaLogRepository.FindByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns((SagaLog?)null);

        // Act
        await _sut.ExecuteCancelSagaAsync("order-1", "ユーザーリクエスト");

        // Assert
        await _orderService.Received(1).CancelOrderAsync(
            "order-1", "ユーザーリクエスト", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_RejectCancel_When_StatusIsShipped()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "SHIPPED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var act = async () => await _sut.ExecuteCancelSagaAsync("order-1", "ユーザーリクエスト");

        // Assert
        var ex = await Should.ThrowAsync<InvalidOrderStateException>(act);
        ex.Message.ShouldContain("SHIPPED");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_RejectCancel_When_StatusIsDelivered()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "DELIVERED");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var act = async () => await _sut.ExecuteCancelSagaAsync("order-1", "ユーザーリクエスト");

        // Assert
        await Should.ThrowAsync<InvalidOrderStateException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_ThrowNotFoundException_When_OrderNotFound()
    {
        // Arrange
        _orderRepository.FindByIdWithDetailsAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        // Act
        var act = async () => await _sut.ExecuteCancelSagaAsync("nonexistent", "ユーザーリクエスト");

        // Assert
        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_SetRefundPending_When_PaymentRefundFails()
    {
        // Arrange
        var order = CreateTestOrder("order-1", "CONFIRMED");
        order.PaymentStatus = "CAPTURED";
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        var checkoutSaga = new SagaLog
        {
            SagaType = "ORDER_CHECKOUT",
            OrderId = "order-1",
            StepResults = """{"paymentId":"pay-1"}"""
        };
        _sagaLogRepository.FindByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(checkoutSaga);

        _paymentClient.RefundPaymentAsync("pay-1", Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalServiceException("返金サービスエラー"));

        // Act
        await _sut.ExecuteCancelSagaAsync("order-1", "ユーザーリクエスト");

        // Assert — 返金失敗でもキャンセル自体は成功する
        await _orderService.Received(1).CancelOrderAsync(
            "order-1", "ユーザーリクエスト", Arg.Any<CancellationToken>());
    }

    private static Order CreateTestOrder(string orderId, string status)
    {
        return new Order
        {
            Id = orderId,
            OrderNumber = $"ORD-TEST-{orderId}",
            CustomerId = "cust-1",
            Status = status,
            PaymentStatus = "PENDING",
            PaymentMethod = "CREDIT_CARD",
            SubtotalAmount = 10000m,
            TotalAmount = 11000m,
            CurrencyCode = "JPY"
        };
    }
}
