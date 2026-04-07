namespace SalesManagementService.Infrastructure.ExternalServices;

public record GetCartResponse(IReadOnlyList<CartItemDto> Items);

public interface ICartClient
{
    Task<GetCartResponse> GetCartAsync(string userId, int deadlineMs, CancellationToken ct = default);

    Task ClearCartAsync(string userId, int deadlineMs, CancellationToken ct = default);
}
