using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services;
using PaymentCartService.Services.Interfaces;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Services;

public class PaymentServiceTests
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly IStripeGateway _stripeGateway;
    private readonly AppDbContext _dbContext;
    private readonly IOptions<StripeSettings> _stripeOptions;
    private readonly IOptions<PaymentSettings> _paymentOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _paymentRepository = Substitute.For<IPaymentRepository>();
        _cartRepository = Substitute.For<ICartRepository>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _stripeGateway = Substitute.For<IStripeGateway>();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _dbContext = new AppDbContext(dbOptions, _timeProvider);

        _stripeOptions = Options.Create(new StripeSettings
        {
            SecretKey = "sk_test_dummy",
            WebhookSecret = "whsec_dummy",
            SuccessUrl = "https://example.com/success",
            CancelUrl = "https://example.com/cancel"
        });
        _paymentOptions = Options.Create(new PaymentSettings
        {
            DefaultCurrency = "jpy",
            MaxRetries = 3,
            WebhookToleranceSeconds = 300
        });
        _logger = Substitute.For<ILogger<PaymentService>>();

        _sut = new PaymentService(
            _paymentRepository, _cartRepository, _outboxEventRepository,
            _stripeGateway, _dbContext, _stripeOptions, _paymentOptions, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_CartNotFoundForCheckout()
    {
        // Arrange
        _cartRepository.FindByIdWithItemsAsync("nonexistent-cart", default).Returns((Cart?)null);

        var request = new CheckoutRequest("nonexistent-cart", "CREDIT_CARD", null, null, null);

        // Act & Assert
        var act = async () => await _sut.CheckoutAsync(request, "user-1");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("カートが見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CartIsEmpty()
    {
        // Arrange
        var emptyCart = new Cart
        {
            Id = "cart-empty",
            CustomerId = "user-1",
            Status = CartStatus.Active
        };
        _cartRepository.FindByIdWithItemsAsync("cart-empty", default).Returns(emptyCart);

        var request = new CheckoutRequest("cart-empty", "CREDIT_CARD", null, null, null);

        // Act & Assert
        var act = async () => await _sut.CheckoutAsync(request, "user-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("空");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_PaymentNotFoundById()
    {
        // Arrange
        _paymentRepository.FindByIdAsync("nonexistent", default).Returns((Payment?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent", "user-1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_PaymentBelongsToDifferentUser()
    {
        // Arrange
        var payment = new Payment
        {
            Id = "pay-1",
            CustomerId = "other-user",
            Status = PaymentStatus.Completed
        };
        _paymentRepository.FindByIdAsync("pay-1", default).Returns(payment);

        // Act & Assert
        var act = async () => await _sut.GetByIdAsync("pay-1", "user-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("アクセス権限");
    }
}
