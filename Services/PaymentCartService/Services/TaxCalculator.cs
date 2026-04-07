using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

/// <summary>
/// 税金計算サービス
/// 将来的に軽減税率、地域別税率計算などを実装予定
/// </summary>
public class TaxCalculator(ILogger<TaxCalculator> logger) : ITaxCalculator
{
    private const decimal StandardTaxRate = 0.10m; // 標準税率 10%

    /// <summary>
    /// 消費税を計算します
    /// </summary>
    /// <param name="subtotal">税抜商品小計</param>
    /// <param name="region">地域コード（将来的に地域別税率計算に使用）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>消費税額</returns>
    /// <remarks>
    /// TODO: 以下の機能を将来実装予定
    /// - 軽減税率（食品等は 8%）
    /// - 商品カテゴリ別の税率適用
    /// - 非課税商品の対応
    /// - 地域別税率（越境 EC 対応）
    /// </remarks>
    public Task<decimal> CalculateTaxAsync(
        decimal subtotal, string? region = null, CancellationToken ct = default)
    {
        // 現時点では標準税率 10% を適用
        var tax = Math.Round(subtotal * StandardTaxRate, 0);

        logger.LogDebug(
            "税金計算: Subtotal={Subtotal}, TaxRate={TaxRate}, Tax={Tax}, Region={Region}",
            subtotal, StandardTaxRate, tax, region ?? "JP");

        return Task.FromResult(tax);
    }
}
