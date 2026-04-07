using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

/// <summary>
/// 送料計算サービス
/// 将来的に配送業者 API との連携、地域別料金計算などを実装予定
/// </summary>
public class ShippingFeeCalculator(ILogger<ShippingFeeCalculator> logger) : IShippingFeeCalculator
{
    /// <summary>
    /// 送料を計算します
    /// </summary>
    /// <param name="subtotal">商品小計</param>
    /// <param name="shippingAddress">配送先住所（将来的に地域別料金計算に使用）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>送料（現時点では固定値を返却）</returns>
    /// <remarks>
    /// TODO: 以下の機能を将来実装予定
    /// - 地域別の送料テーブル
    /// - 配送業者 API との連携
    /// - 送料無料の閾値設定
    /// - 商品重量・サイズに基づく計算
    /// </remarks>
    public Task<decimal> CalculateShippingFeeAsync(
        decimal subtotal, string? shippingAddress = null, CancellationToken ct = default)
    {
        // 現時点ではシンプルな送料計算（10,000円以上で無料）
        var shippingFee = subtotal >= 10000m ? 0m : 800m;

        logger.LogDebug(
            "送料計算: Subtotal={Subtotal}, ShippingFee={ShippingFee}, Address={Address}",
            subtotal, shippingFee, shippingAddress ?? "未指定");

        return Task.FromResult(shippingFee);
    }
}
