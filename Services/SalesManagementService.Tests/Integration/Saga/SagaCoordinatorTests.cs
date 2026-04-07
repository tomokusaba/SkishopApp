using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SalesManagementService.Configurations;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.ExternalServices;
using SalesManagementService.Infrastructure.Saga;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;
using Shouldly;

namespace SalesManagementService.Tests.Integration.Saga;

public class SagaCoordinatorTests
{
    private readonly ICartClient _cartClient;
    private readonly IInventoryClient _inventoryClient;
    private readonly ICouponClient _couponClient;
    private readonly IPointClient _pointClient;
    private readonly IPaymentClient _paymentClient;
    private readonly IOrderService _orderService;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ISagaLogRepository _sagaLogRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<SagaCoordinator> _logger;
    private readonly SagaCoordinator _sut;

    public SagaCoordinatorTests()
    {
        _cartClient = Substitute.For<ICartClient>();
        _inventoryClient = Substitute.For<IInventoryClient>();
        _couponClient = Substitute.For<ICouponClient>();
        _pointClient = Substitute.For<IPointClient>();
        _paymentClient = Substitute.For<IPaymentClient>();
        _orderService = Substitute.For<IOrderService>();
        _outboxWriter = Substitute.For<IOutboxWriter>();
        _sagaLogRepository = Substitute.For<ISagaLogRepository>();
        _idempotencyKeyRepository = Substitute.For<IIdempotencyKeyRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<SagaCoordinator>>();

        var sagaSettings = new SagaSettings(
            SloDeadlineMs: 1000,
            CompensationTimeoutSeconds: 30,
            RecoveryPollingIntervalSeconds: 30,
            StallThresholdMinutes: 5,
            PaymentPollingTimeoutMinutes: 30);

        _sut = new SagaCoordinator(
            _cartClient, _inventoryClient, _couponClient, _pointClient,
            _paymentClient, _orderService, _outboxWriter,
            _sagaLogRepository, _idempotencyKeyRepository, _timeProvider, Options.Create(sagaSettings), _logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_CompleteAllSteps_When_AllServicesSucceed()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        var userId = "user-1";
        var idempotencyKey = "idem-key-1";

        SetupSuccessfulSagaSteps(request, userId);

        // Act
        var result = await _sut.ExecuteCheckoutSagaAsync(request, userId, idempotencyKey);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("order-1");
        await _cartClient.Received(1).GetCartAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _inventoryClient.Received(1).ReserveInventoryAsync(
            Arg.Any<IReadOnlyList<CartItemDto>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _paymentClient.Received(1).ProcessPaymentAsync(
            Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_CompensateInReverseOrder_When_PaymentFails()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { CouponCode = "COUPON-1", UsedPoints = 100 };
        var userId = "user-1";

        SetupSuccessfulSagaSteps(request, userId);

        _paymentClient.ProcessPaymentAsync(
            Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentProcessingException("決済処理に失敗しました"));

        // Act
        var act = async () => await _sut.ExecuteCheckoutSagaAsync(request, userId, "idem-2");

        // Assert
        await Should.ThrowAsync<PaymentProcessingException>(act);

        // 補償: 注文キャンセル(step5), ポイント解放(step4), クーポン解放(step3), 在庫解放(step2)
        await _orderService.Received(1).CancelOrderAsync(
            "order-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _pointClient.Received(1).ReleasePointsAsync(
            "point-res-1", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _inventoryClient.Received(1).ReleaseReservationAsync(
            "res-1", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_CompensateOnlyCompletedSteps_When_Step3Fails()
    {
        // Arrange — クーポン検証（Step3）で失敗
        var request = CreateValidOrderRequest() with { CouponCode = "INVALID_COUPON" };
        var userId = "user-1";

        _cartClient.GetCartAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new GetCartResponse(new List<CartItemDto>
            {
                new("prod-1", "テスト商品", 10000m, 1)
            }));
        _inventoryClient.ReserveInventoryAsync(
            Arg.Any<IReadOnlyList<CartItemDto>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ReserveInventoryResponse("res-1"));
        _couponClient.ValidateCouponAsync(
            "INVALID_COUPON", userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessException("無効なクーポンです"));

        // Act
        var act = async () => await _sut.ExecuteCheckoutSagaAsync(request, userId, "idem-3");

        // Assert
        await Should.ThrowAsync<BusinessException>(act);

        // Step 5 (注文)は未到達のため補償なし
        await _orderService.DidNotReceive().CancelOrderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Step 2 (在庫)は完了済みのため補償
        await _inventoryClient.Received(1).ReleaseReservationAsync(
            "res-1", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_NotCompensatePostProcessingSteps_When_Step7Fails()
    {
        // Arrange — Step 7 (ポイント付与)は後処理ステップのため、失敗しても Saga は完了
        var request = CreateValidOrderRequest();
        var userId = "user-1";

        SetupSuccessfulSagaSteps(request, userId);

        _pointClient.AwardPointsAsync(
            userId, Arg.Any<decimal>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalServiceException("ポイントサービスが利用できません"));

        // Act
        var result = await _sut.ExecuteCheckoutSagaAsync(request, userId, "idem-4");

        // Assert — Saga は正常完了する（後処理の失敗は無視）
        result.ShouldNotBeNull();
        result.Id.ShouldBe("order-1");
    }

    private void SetupSuccessfulSagaSteps(OrderCreateRequest request, string userId)
    {
        _cartClient.GetCartAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new GetCartResponse(new List<CartItemDto>
            {
                new("prod-1", "テスト商品", 10000m, 1)
            }));

        _inventoryClient.ReserveInventoryAsync(
            Arg.Any<IReadOnlyList<CartItemDto>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ReserveInventoryResponse("res-1"));

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            _couponClient.ValidateCouponAsync(
                request.CouponCode, userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValidateCouponResponse(500m, "coupon-1"));
        }

        if (request.UsedPoints > 0)
        {
            _pointClient.ReservePointsAsync(
                userId, request.UsedPoints, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ReservePointsResponse("point-res-1"));
        }

        var orderDetail = new OrderDetailDto(
            "order-1", "ORD-20260318-00001", userId,
            DateTimeOffset.UtcNow, "PENDING", "PENDING",
            "CREDIT_CARD", 10000m, 1000m, 550m, 0m, 11550m,
            null, 0, 0m,
            "100-0001", "東京都", "千代田区", "番地1-1", null,
            "テスト太郎", "03-1234-5678", "JPY",
            null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            new List<OrderItemDto>
            {
                new("item-1", "prod-1", "テスト商品", "SKU-001", 10000m, 1, 10000m)
            });

        _orderService.CreateOrderAsync(
            Arg.Any<OrderCreateRequest>(), Arg.Any<SagaContext>(), Arg.Any<CancellationToken>())
            .Returns(orderDetail);

        _paymentClient.ProcessPaymentAsync(
            Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentResult("pay-1", "CAPTURED"));
    }

    private static OrderCreateRequest CreateValidOrderRequest()
    {
        return new OrderCreateRequest(
            "cust-1",
            new List<OrderItemRequest>
            {
                new("prod-1", "テスト商品", "SKU-001", 10000m, 1)
            },
            new ShippingAddressRequest(
                "テスト太郎", "100-0001", "東京都", "千代田区", "番地1-1", null, "03-1234-5678"),
            "CREDIT_CARD");
    }
}
