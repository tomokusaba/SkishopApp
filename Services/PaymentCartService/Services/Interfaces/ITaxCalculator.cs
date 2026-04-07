namespace PaymentCartService.Services.Interfaces;

public interface ITaxCalculator
{
    Task<decimal> CalculateTaxAsync(decimal subtotal, string? region = null, CancellationToken ct = default);
}
