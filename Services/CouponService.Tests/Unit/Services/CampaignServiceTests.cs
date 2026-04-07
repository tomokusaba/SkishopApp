using CouponService.DTOs.Requests;
using CouponService.Exceptions;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CouponService.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class CampaignServiceTests
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly CampaignAppService _service;

    public CampaignServiceTests()
    {
        _campaignRepository = Substitute.For<ICampaignRepository>();
        var logger = Substitute.For<ILogger<CampaignAppService>>();
        _service = new CampaignAppService(_campaignRepository, TimeProvider.System, logger);
    }

    [Fact]
    public async Task Should_CreateCampaign_When_ValidRequest()
    {
        // Arrange
        var request = new CreateCampaignRequest(
            "夏のセール", "夏季限定キャンペーン",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(30), 1000);

        // Act
        var result = await _service.CreateCampaignAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("夏のセール");
        await _campaignRepository.Received(1).AddAsync(Arg.Any<Campaign>(), default);
        await _campaignRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Should_ReturnCampaign_When_GetByIdExists()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = "camp-1", Name = "テストキャンペーン",
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(30),
            MaxCoupons = 100
        };
        _campaignRepository.FindByIdAsync("camp-1", default).Returns(campaign);

        // Act
        var result = await _service.GetByIdAsync("camp-1");

        // Assert
        result.ShouldNotBeNull();
        result!.Name.ShouldBe("テストキャンペーン");
    }

    [Fact]
    public async Task Should_ReturnNull_When_CampaignNotFound()
    {
        // Arrange
        _campaignRepository.FindByIdAsync("missing", default).Returns((Campaign?)null);

        // Act
        var result = await _service.GetByIdAsync("missing");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ActivateCampaign_When_Draft()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = "camp-1", Name = "テスト",
            Status = CampaignStatus.Draft,
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(30),
            MaxCoupons = 100
        };
        _campaignRepository.FindByIdAsync("camp-1", default).Returns(campaign);

        // Act
        await _service.ActivateCampaignAsync("camp-1");

        // Assert
        campaign.Status.ShouldBe(CampaignStatus.Active);
        await _campaignRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_ActivateNonExistentCampaign()
    {
        // Arrange
        _campaignRepository.FindByIdAsync("missing", default).Returns((Campaign?)null);

        // Act & Assert
        var act = async () => await _service.ActivateCampaignAsync("missing");
        var ex = await Should.ThrowAsync<CouponNotFoundException>(act);
        ex.Message.ShouldContain("missing");
    }

    [Fact]
    public async Task Should_PauseCampaign_When_Active()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = "camp-1", Name = "テスト",
            Status = CampaignStatus.Active,
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(30),
            MaxCoupons = 100
        };
        _campaignRepository.FindByIdAsync("camp-1", default).Returns(campaign);

        // Act
        await _service.PauseCampaignAsync("camp-1");

        // Assert
        campaign.Status.ShouldBe(CampaignStatus.Paused);
        await _campaignRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Should_UpdateCampaign_When_ValidRequest()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = "camp-1", Name = "旧名",
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(30),
            MaxCoupons = 100
        };
        _campaignRepository.FindByIdAsync("camp-1", default).Returns(campaign);
        var request = new UpdateCampaignRequest("新名", "更新後の説明", null, null, null);

        // Act
        var result = await _service.UpdateCampaignAsync("camp-1", request);

        // Assert
        result.Name.ShouldBe("新名");
        await _campaignRepository.Received(1).SaveChangesAsync(default);
    }
}
