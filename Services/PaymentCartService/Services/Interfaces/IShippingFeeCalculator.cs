namespace PaymentCartService.Services.Interfaces;

public interface IShippingFeeCalculator
{
    Task<decimal> CalculateShippingFeeAsync(
        decimal subtotal, string? shippingAddress = null, CancellationToken ct = default);
}
