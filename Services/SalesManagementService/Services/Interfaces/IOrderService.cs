using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Saga;
using SalesManagementService.Repositories;

namespace SalesManagementService.Services.Interfaces;

public interface IOrderService
{
    Task<OrderDetailDto> CreateOrderAsync(OrderCreateRequest request, SagaContext context, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByIdAndUserIdAsync(string id, string userId, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<PaginatedResult<OrderDto>> GetByCustomerIdAsync(string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<OrderDto>> SearchAsync(string? customerId, string? status, string? paymentStatus, int page, int pageSize, CancellationToken ct = default);
    Task CancelOrderAsync(string orderId, string reason, CancellationToken ct = default);
    Task UpdateStatusAsync(string orderId, string newStatus, CancellationToken ct = default);
}
