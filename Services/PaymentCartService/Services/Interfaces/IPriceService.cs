namespace PaymentCartService.Services.Interfaces;

public interface IPriceService
{
    Task<decimal> CalculateTotalAsync(string cartId, CancellationToken ct = default);
}
