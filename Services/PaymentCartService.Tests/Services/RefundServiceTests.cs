using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
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

public class RefundServiceTests
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly IStripeGateway _stripeGateway;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefundService> _logger;
    private readonly RefundService _sut;

    public RefundServiceTests()
    {
        _paymentRepository = Substitute.For<IPaymentRepository>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _stripeGateway = Substitute.For<IStripeGateway>();
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<RefundService>>();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(dbOptions, _timeProvider);
        _sut = new RefundService(
            _paymentRepository, _outboxEventRepository, _stripeGateway, dbContext, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_PaymentNotFoundForRefund()
    {
        // Arrange
        _paymentRepository.FindByIdAsync("nonexistent", default).Returns((Payment?)null);
        var request = new RefundRequest(10000m, "返品のため");

        // Act & Assert
        var act = async () => await _sut.RefundAsync("nonexistent", request, "admin-1");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("決済が見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_PaymentNotCompleted()
    {
        // Arrange
        var payment = new Payment
        {
            Id = "pay-1",
            Status = PaymentStatus.Pending,
            Amount = 10000m
        };
        _paymentRepository.FindByIdAsync("pay-1", default).Returns(payment);
        var request = new RefundRequest(10000m, "返品のため");

        // Act & Assert
        var act = async () => await _sut.RefundAsync("pay-1", request, "admin-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("完了");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_RefundAmountExceedsPayment()
    {
        // Arrange
        var payment = new Payment
        {
            Id = "pay-1",
            Status = PaymentStatus.Completed,
            Amount = 10000m
        };
        _paymentRepository.FindByIdAsync("pay-1", default).Returns(payment);
        var request = new RefundRequest(15000m, "返品のため");

        // Act & Assert
        var act = async () => await _sut.RefundAsync("pay-1", request, "admin-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("超過");
    }
}
