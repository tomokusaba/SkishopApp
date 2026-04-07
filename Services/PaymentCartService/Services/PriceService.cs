using PaymentCartService.Exceptions;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class PriceService(
    ICartRepository cartRepository,
    ILogger<PriceService> logger) : IPriceService
{
    public async Task<decimal> CalculateTotalAsync(string cartId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        var total = cart.CalculateTotal();
        logger.LogInformation("合計計算: CartId={CartId}, Total={Total}", cartId, total);
        return total;
    }
}
