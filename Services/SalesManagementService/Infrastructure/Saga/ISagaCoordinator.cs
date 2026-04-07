using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Models;

namespace SalesManagementService.Infrastructure.Saga;

public interface ISagaCoordinator
{
    Task<OrderDetailDto> ExecuteCheckoutSagaAsync(
        OrderCreateRequest request, string userId, string idempotencyKey, CancellationToken ct = default);

    Task CompensateAsync(SagaLog sagaLog, CancellationToken ct = default);
}
