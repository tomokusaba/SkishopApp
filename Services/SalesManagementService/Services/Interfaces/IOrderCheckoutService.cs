using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;

namespace SalesManagementService.Services.Interfaces;

public interface IOrderCheckoutService
{
    Task<OrderDetailDto> ExecuteCheckoutAsync(
        OrderCreateRequest request,
        string userId,
        string idempotencyKey,
        CancellationToken ct = default);
}
