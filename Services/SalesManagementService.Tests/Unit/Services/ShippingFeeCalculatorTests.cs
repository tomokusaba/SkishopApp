using Microsoft.Extensions.Options;
using SalesManagementService.Configurations;
using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class ShippingFeeCalculatorTests
{
    private readonly ShippingFeeCalculator _calculator;

    public ShippingFeeCalculatorTests()
    {
        var settings = new ShippingSettings(
            FreeShippingThreshold: 10000,
            DefaultShippingFee: 660,
            HokkaidoOkinawaFee: 1100,
            ExpressSurcharge: 330,
            LargeItemSurcharge: 1650,
            MemberRankDiscounts: new Dictionary<string, decimal>
            {
                ["Silver"] = 8000,
                ["Gold"] = 5000,
                ["Platinum"] = 0
            });
        _calculator = new ShippingFeeCalculator(Options.Create(settings));
    }

    // ── Standard 会員 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Return660_When_StandardMemberBelowThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(9999m, "Standard");

        // Assert
        fee.ShouldBe(660m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_StandardMemberAtThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(10000m, "Standard");

        // Assert
        fee.ShouldBe(0m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_StandardMemberAboveThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(15000m, "Standard");

        // Assert
        fee.ShouldBe(0m);
    }

    // ── Silver 会員 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Return660_When_SilverMemberBelowThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(7999m, "Silver");

        // Assert
        fee.ShouldBe(660m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_SilverMemberAtThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(8000m, "Silver");

        // Assert
        fee.ShouldBe(0m);
    }

    // ── Gold 会員 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Return660_When_GoldMemberBelowThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(4999m, "Gold");

        // Assert
        fee.ShouldBe(660m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_GoldMemberAtThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Gold");

        // Assert
        fee.ShouldBe(0m);
    }

    // ── Platinum 会員 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_PlatinumMember()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(100m, "Platinum");

        // Assert
        fee.ShouldBe(0m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_PlatinumMemberZeroSubtotal()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(0m, "Platinum");

        // Assert
        fee.ShouldBe(0m);
    }

    // ── 地域別加算 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_AddHokkaidoSurcharge_When_HokkaidoPrefecture()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", "北海道");

        // Assert
        fee.ShouldBe(660m + 1100m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_AddOkinawaSurcharge_When_OkinawaPrefecture()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", "沖縄県");

        // Assert
        fee.ShouldBe(660m + 1100m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_NotAddSurcharge_When_TokyoPrefecture()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", "東京都");

        // Assert
        fee.ShouldBe(660m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_NotAddSurcharge_When_NullPrefecture()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", null);

        // Assert
        fee.ShouldBe(660m);
    }

    // ── お急ぎ便 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_AddExpressSurcharge_When_ExpressShipping()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", isExpress: true);

        // Assert
        fee.ShouldBe(660m + 330m);
    }

    // ── 大型商品 ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_AddOversizedSurcharge_When_HasOversizedItem()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(5000m, "Standard", hasOversizedItem: true);

        // Assert
        fee.ShouldBe(660m + 1650m);
    }

    // ── 複合テスト: 加算が重複する ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_StackAllSurcharges_When_ExpressOversizedHokkaido()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(
            5000m, "Standard", "北海道", isExpress: true, hasOversizedItem: true);

        // Assert — base(660) + 北海道(1100) + express(330) + oversized(1650) = 3740
        fee.ShouldBe(660m + 1100m + 330m + 1650m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_OnlyChargeSurcharges_When_PlatinumExpressOversizedHokkaido()
    {
        // Arrange — Platinum は基本料無料だがサーチャージは発生する
        // Act
        var fee = _calculator.Calculate(
            100m, "Platinum", "北海道", isExpress: true, hasOversizedItem: true);

        // Assert — 北海道(1100) + express(330) + oversized(1650) = 3080
        fee.ShouldBe(1100m + 330m + 1650m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_OnlyChargeSurcharges_When_FreeShippingWithExpressAndRegion()
    {
        // Arrange — subtotal >= 10000 で基本送料無料、ただしサーチャージは課金
        // Act
        var fee = _calculator.Calculate(
            15000m, "Standard", "沖縄県", isExpress: true);

        // Assert — base(0) + 沖縄(1100) + express(330) = 1430
        fee.ShouldBe(1100m + 330m);
    }

    // ── デフォルト会員ランク ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_UseStandardThreshold_When_UnknownMemberRank()
    {
        // Arrange — 不明なランクはデフォルト FreeShippingThreshold (10000) を使用
        // Act
        var fee = _calculator.Calculate(9999m, "Unknown");

        // Assert
        fee.ShouldBe(660m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFree_When_UnknownMemberRankAboveThreshold()
    {
        // Arrange & Act
        var fee = _calculator.Calculate(10000m, "Unknown");

        // Assert
        fee.ShouldBe(0m);
    }
}
