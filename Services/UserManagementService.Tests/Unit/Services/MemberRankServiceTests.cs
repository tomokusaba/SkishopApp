using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Tests.Unit.Services;

public class MemberRankServiceTests
{
    private readonly IMemberRankRepository _memberRankRepository;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MemberRankService> _logger;
    private readonly MemberRankService _sut;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);

    public MemberRankServiceTests()
    {
        _memberRankRepository = Substitute.For<IMemberRankRepository>();
        _processedEventRepository = Substitute.For<IProcessedEventRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _timeProvider = Substitute.For<TimeProvider>();
        _logger = Substitute.For<ILogger<MemberRankService>>();

        _timeProvider.GetUtcNow().Returns(FixedNow);

        _sut = new MemberRankService(
            _memberRankRepository, _processedEventRepository, _eventPublisher, _timeProvider, _logger);
    }

    private static MemberRank CreateTestRank(
        string userId = "user-1",
        string rank = MemberRankLevel.Bronze,
        decimal annualAmount = 0m,
        decimal previousYearAmount = 0m,
        decimal pointRate = 0.01m) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserId = userId,
        CurrentRank = rank,
        AnnualPurchaseAmount = annualAmount,
        PreviousYearAmount = previousYearAmount,
        PointRate = pointRate,
        RankUpdatedAt = FixedNow.AddMonths(-3),
        NextEvaluationDate = new DateOnly(2026, 4, 1)
    };

    #region InitializeAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateBronzeRank_When_UserHasNoRank()
    {
        // Arrange
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((MemberRank?)null);

        // Act
        await _sut.InitializeAsync("user-1");

        // Assert
        await _memberRankRepository.Received(1).AddAsync(
            Arg.Is<MemberRank>(r =>
                r.UserId == "user-1" &&
                r.CurrentRank == MemberRankLevel.Bronze &&
                r.PointRate == 0.01m &&
                r.NextEvaluationDate == new DateOnly(2027, 4, 1)),
            Arg.Any<CancellationToken>());
        await _memberRankRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipInitialization_When_RankAlreadyExists()
    {
        // Arrange
        var existingRank = CreateTestRank();
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(existingRank);

        // Act
        await _sut.InitializeAsync("user-1");

        // Assert
        await _memberRankRepository.DidNotReceive().AddAsync(Arg.Any<MemberRank>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetByUserIdAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnMemberRankDto_When_RankExists()
    {
        // Arrange
        var rank = CreateTestRank(rank: MemberRankLevel.Gold, annualAmount: 120_000m, pointRate: 0.05m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBe("user-1");
        result.CurrentRank.ShouldBe(MemberRankLevel.Gold);
        result.AnnualPurchaseAmount.ShouldBe(120_000m);
        result.PointRate.ShouldBe(0.05m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_RankNotFound()
    {
        // Arrange
        _memberRankRepository.FindByUserIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((MemberRank?)null);

        // Act
        var result = await _sut.GetByUserIdAsync("no-user");

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region AddPurchaseAmountAsync — Promotion Thresholds

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(49_999, MemberRankLevel.Bronze, 0.01)]
    [InlineData(50_000, MemberRankLevel.Silver, 0.03)]
    [InlineData(50_001, MemberRankLevel.Silver, 0.03)]
    [InlineData(99_999, MemberRankLevel.Silver, 0.03)]
    [InlineData(100_000, MemberRankLevel.Gold, 0.05)]
    [InlineData(100_001, MemberRankLevel.Gold, 0.05)]
    [InlineData(299_999, MemberRankLevel.Gold, 0.05)]
    [InlineData(300_000, MemberRankLevel.Platinum, 0.07)]
    [InlineData(300_001, MemberRankLevel.Platinum, 0.07)]
    public async Task Should_PromoteToCorrectRank_When_ThresholdReached(
        double purchaseAmount, string expectedRank, double expectedPointRate)
    {
        // Arrange
        var amount = (decimal)purchaseAmount;
        var rank = CreateTestRank(annualAmount: 0m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        await _sut.AddPurchaseAmountAsync("user-1", "test-order-id", amount);

        // Assert
        rank.CurrentRank.ShouldBe(expectedRank);
        rank.PointRate.ShouldBe((decimal)expectedPointRate);
        await _memberRankRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PublishEvent_When_PromotionOccurs()
    {
        // Arrange
        var rank = CreateTestRank(annualAmount: 0m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        await _sut.AddPurchaseAmountAsync("user-1", "test-order-id", 50_000m);

        // Assert
        await _eventPublisher.Received(1).PublishMemberRankUpdatedAsync(
            "user-1", MemberRankLevel.Bronze, MemberRankLevel.Silver, 0.03m,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotPublishEvent_When_NoPromotion()
    {
        // Arrange
        var rank = CreateTestRank(annualAmount: 0m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        await _sut.AddPurchaseAmountAsync("user-1", "test-order-id", 10_000m);

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Bronze);
        await _eventPublisher.DidNotReceive().PublishMemberRankUpdatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotDemote_When_PurchaseAmountAccumulatedAboveCurrentRank()
    {
        // Arrange — user is already Silver with 60k, add small amount
        var rank = CreateTestRank(rank: MemberRankLevel.Silver, annualAmount: 60_000m, pointRate: 0.03m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        await _sut.AddPurchaseAmountAsync("user-1", "test-order-id", 1_000m);

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Silver);
        rank.AnnualPurchaseAmount.ShouldBe(61_000m);
        await _eventPublisher.DidNotReceive().PublishMemberRankUpdatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_RankNotFoundOnPurchase()
    {
        // Arrange
        _memberRankRepository.FindByUserIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((MemberRank?)null);

        // Act
        var act = async () => await _sut.AddPurchaseAmountAsync("no-user", "test-order-id", 10_000m);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-user");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AccumulatePurchaseAmount_When_MultipleAdditions()
    {
        // Arrange
        var rank = CreateTestRank(annualAmount: 40_000m);
        _memberRankRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(rank);

        // Act
        await _sut.AddPurchaseAmountAsync("user-1", "test-order-id", 10_000m);

        // Assert
        rank.AnnualPurchaseAmount.ShouldBe(50_000m);
        rank.CurrentRank.ShouldBe(MemberRankLevel.Silver);
    }

    #endregion

    #region EvaluateAllRanksAsync — Annual Evaluation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DemoteByOneRank_When_AnnualAmountDropsByMoreThanOneLevel()
    {
        // Arrange — Platinum with only 20k annual (would be Bronze, but 1-rank limit → Gold)
        var rank = CreateTestRank(
            rank: MemberRankLevel.Platinum, annualAmount: 20_000m, pointRate: 0.07m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Gold);
        rank.PointRate.ShouldBe(0.05m);
        rank.AnnualPurchaseAmount.ShouldBe(0m);
        rank.PreviousYearAmount.ShouldBe(20_000m);
        await _eventPublisher.Received(1).PublishMemberRankUpdatedAsync(
            rank.UserId, MemberRankLevel.Platinum, MemberRankLevel.Gold, 0.05m,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MaintainPlatinum_When_PreviousYearAmountAboveGraceThreshold()
    {
        // Arrange — Platinum with 250k annual (grace threshold)
        var rank = CreateTestRank(
            rank: MemberRankLevel.Platinum, annualAmount: 250_000m, pointRate: 0.07m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Platinum);
        rank.PointRate.ShouldBe(0.07m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DemotePlatinum_When_PreviousYearAmountBelowGraceThreshold()
    {
        // Arrange — Platinum with 249,999 annual (just below grace threshold)
        var rank = CreateTestRank(
            rank: MemberRankLevel.Platinum, annualAmount: 249_999m, pointRate: 0.07m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Gold);
        rank.PointRate.ShouldBe(0.05m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ResetAnnualAmount_When_EvaluationCompleted()
    {
        // Arrange
        var rank = CreateTestRank(
            rank: MemberRankLevel.Silver, annualAmount: 60_000m, pointRate: 0.03m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.AnnualPurchaseAmount.ShouldBe(0m);
        rank.PreviousYearAmount.ShouldBe(60_000m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotPublishEvent_When_RankUnchangedAfterEvaluation()
    {
        // Arrange — Silver with 55k keeps Silver
        var rank = CreateTestRank(
            rank: MemberRankLevel.Silver, annualAmount: 55_000m, pointRate: 0.03m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Silver);
        await _eventPublisher.DidNotReceive().PublishMemberRankUpdatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DemoteGoldToBronze_ByOneStepOnly()
    {
        // Arrange — Gold user with 10k (would be Bronze, limited to Silver)
        var rank = CreateTestRank(
            rank: MemberRankLevel.Gold, annualAmount: 10_000m, pointRate: 0.05m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Silver);
        rank.PointRate.ShouldBe(0.03m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DemoteSilverToBronze_WhenAmountZero()
    {
        // Arrange — Silver with 0 annual (demote by exactly 1 rank)
        var rank = CreateTestRank(
            rank: MemberRankLevel.Silver, annualAmount: 0m, pointRate: 0.03m);
        rank.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.CurrentRank.ShouldBe(MemberRankLevel.Bronze);
        rank.PointRate.ShouldBe(0.01m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetNextEvaluationDateOneYearAhead_When_Evaluated()
    {
        // Arrange
        var evaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        var rank = CreateTestRank(
            rank: MemberRankLevel.Bronze, annualAmount: 10_000m);
        rank.NextEvaluationDate = evaluationDate;
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank.NextEvaluationDate.ShouldBe(evaluationDate.AddYears(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ProcessMultipleRanks_When_BatchEvaluated()
    {
        // Arrange
        var rank1 = CreateTestRank("user-1", MemberRankLevel.Bronze, 10_000m);
        rank1.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        var rank2 = CreateTestRank("user-2", MemberRankLevel.Gold, 120_000m, pointRate: 0.05m);
        rank2.NextEvaluationDate = DateOnly.FromDateTime(FixedNow.UtcDateTime);
        _memberRankRepository.FindAllForEvaluationAsync(
                Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new List<MemberRank> { rank1, rank2 }),
                Task.FromResult(new List<MemberRank>()));

        // Act
        await _sut.EvaluateAllRanksAsync();

        // Assert
        rank1.AnnualPurchaseAmount.ShouldBe(0m);
        rank2.AnnualPurchaseAmount.ShouldBe(0m);
        await _memberRankRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
