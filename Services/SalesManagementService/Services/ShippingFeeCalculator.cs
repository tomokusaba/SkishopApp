using Microsoft.Extensions.Options;
using SalesManagementService.Configurations;

namespace SalesManagementService.Services;

/// <summary>
/// 配送料計算（設計書 §5 準拠）
/// 会員ランク別無料閾値: 設定ファイルで管理（デフォルト: Standard≥10000, Silver≥8000, Gold≥5000, Platinum=常時無料）
/// 地域別加算: 北海道・沖縄
/// お急ぎ便: +ExpressSurcharge
/// 大型商品: +LargeItemSurcharge
/// </summary>
public class ShippingFeeCalculator(IOptions<ShippingSettings> shippingOptions)
{
    private readonly ShippingSettings _settings = shippingOptions.Value;

    public decimal Calculate(
        decimal subtotal,
        string memberRank = "Standard",
        string? prefecture = null,
        bool isExpress = false,
        bool hasOversizedItem = false)
    {
        if (memberRank == "Platinum")
            return CalculateSurcharges(prefecture, isExpress, hasOversizedItem);

        var threshold = GetFreeShippingThreshold(memberRank);
        var baseFee = subtotal >= threshold ? 0m : _settings.DefaultShippingFee;

        return baseFee + CalculateSurcharges(prefecture, isExpress, hasOversizedItem);
    }

    private decimal GetFreeShippingThreshold(string memberRank)
    {
        if (_settings.MemberRankDiscounts is not null
            && _settings.MemberRankDiscounts.TryGetValue(memberRank, out var rankThreshold))
        {
            return rankThreshold;
        }

        return _settings.FreeShippingThreshold;
    }

    private decimal CalculateSurcharges(
        string? prefecture, bool isExpress, bool hasOversizedItem)
    {
        var surcharge = 0m;

        if (prefecture is "北海道" or "沖縄県")
            surcharge += _settings.HokkaidoOkinawaFee;

        if (isExpress)
            surcharge += _settings.ExpressSurcharge;

        if (hasOversizedItem)
            surcharge += _settings.LargeItemSurcharge;

        return surcharge;
    }
}
