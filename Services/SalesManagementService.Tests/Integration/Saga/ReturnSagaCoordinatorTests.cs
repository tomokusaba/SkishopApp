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

public class ReturnSagaCoordinatorTests
{
    private readonly ISagaLogRepository _sagaLogRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly IPaymentClient _paymentClient;
    private readonly IInventoryClient _inventoryClient;
    private readonly IPointClient _pointClient;
    private readonly IOutboxWriter _outboxWriter;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<ReturnSagaCoordinator> _logger;
    private readonly ReturnSagaCoordinator _sut;

    public ReturnSagaCoordinatorTests()
    {
        _sagaLogRepository = Substitute.For<ISagaLogRepository>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _returnRepository = Substitute.For<IReturnRepository>();
        _paymentClient = Substitute.For<IPaymentClient>();
        _inventoryClient = Substitute.For<IInventoryClient>();
        _pointClient = Substitute.For<IPointClient>();
        _outboxWriter = Substitute.For<IOutboxWriter>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<ReturnSagaCoordinator>>();

        _sut = new ReturnSagaCoordinator(
            _sagaLogRepository, _orderRepository, _returnRepository,
            _paymentClient, _inventoryClient, _pointClient,
            _outboxWriter, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_ProcessReturn_When_StatusIsReceived()
    {
        // Arrange
        var returnEntity = CreateTestReturn("ret-1", "order-1", "RECEIVED");
        var order = CreateTestOrderWithItem("order-1", "cust-1");

        _returnRepository.FindByIdAsync("ret-1", Arg.Any<CancellationToken>())
            .Returns(returnEntity);
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

        // Act
        await _sut.ExecuteReturnSagaAsync("ret-1", "order-1");

        // Assert
        returnEntity.Status.ShouldBe("REFUNDED");
        returnEntity.RefundedAt.ShouldNotBeNull();
        await _paymentClient.Received(1).RefundPaymentAsync(
            "pay-1", Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _outboxWriter.Received(1).WriteAsync(
            "order.return.processed", "order-1",
            Arg.Any<ReturnProcessedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_RejectReturn_When_StatusIsNotReceived()
    {
        // Arrange
        var returnEntity = CreateTestReturn("ret-1", "order-1", "REQUESTED");
        _returnRepository.FindByIdAsync("ret-1", Arg.Any<CancellationToken>())
            .Returns(returnEntity);

        var order = CreateTestOrderWithItem("order-1", "cust-1");
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        var act = async () => await _sut.ExecuteReturnSagaAsync("ret-1", "order-1");

        // Assert
        var ex = await Should.ThrowAsync<InvalidOrderStateException>(act);
        ex.Message.ShouldContain("REQUESTED");
        ex.Message.ShouldContain("RECEIVED");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_ThrowNotFoundException_When_ReturnNotFound()
    {
        // Arrange
        _returnRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Return?)null);

        // Act
        var act = async () => await _sut.ExecuteReturnSagaAsync("nonexistent", "order-1");

        // Assert
        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_ThrowNotFoundException_When_OrderNotFoundForReturn()
    {
        // Arrange
        var returnEntity = CreateTestReturn("ret-1", "order-1", "RECEIVED");
        _returnRepository.FindByIdAsync("ret-1", Arg.Any<CancellationToken>())
            .Returns(returnEntity);
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns((Order?)null);

        // Act
        var act = async () => await _sut.ExecuteReturnSagaAsync("ret-1", "order-1");

        // Assert
        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_SetRefundPending_When_PaymentIdNotFound()
    {
        // Arrange
        var returnEntity = CreateTestReturn("ret-1", "order-1", "RECEIVED");
        var order = CreateTestOrderWithItem("order-1", "cust-1");

        _returnRepository.FindByIdAsync("ret-1", Arg.Any<CancellationToken>())
            .Returns(returnEntity);
        _orderRepository.FindByIdWithDetailsAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(order);
        _sagaLogRepository.FindByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns((SagaLog?)null);

        // Act
        await _sut.ExecuteReturnSagaAsync("ret-1", "order-1");

        // Assert — paymentId がないので RefundPayment は呼ばれない
        await _paymentClient.DidNotReceive().RefundPaymentAsync(
            Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        // しかし Saga 自体は完了する
        await _outboxWriter.Received(1).WriteAsync(
            "order.return.processed", "order-1",
            Arg.Any<ReturnProcessedEvent>(), Arg.Any<CancellationToken>());
    }

    private static Return CreateTestReturn(string returnId, string orderId, string status)
    {
        return new Return
        {
            Id = returnId,
            ReturnNumber = "RTN-20260318-00001",
            OrderId = orderId,
            OrderItemId = "item-1",
            CustomerId = "cust-1",
            Reason = "DEFECTIVE",
            Quantity = 1,
            RefundAmount = 10000m,
            Status = status,
            RequestedAt = DateTimeOffset.UtcNow
        };
    }

    private static Order CreateTestOrderWithItem(string orderId, string customerId)
    {
        var order = new Order
        {
            Id = orderId,
            OrderNumber = $"ORD-TEST-{orderId}",
            CustomerId = customerId,
            Status = "DELIVERED",
            PaymentStatus = "CAPTURED",
            PaymentMethod = "CREDIT_CARD",
            SubtotalAmount = 10000m,
            TotalAmount = 11000m,
            CurrencyCode = "JPY"
        };
        order.Items.Add(new OrderItem
        {
            Id = "item-1",
            OrderId = orderId,
            ProductId = "prod-1",
            ProductName = "テストスキー板",
            Sku = "SKI-001",
            UnitPrice = 10000m,
            Quantity = 1,
            Subtotal = 10000m
        });
        return order;
    }
}
