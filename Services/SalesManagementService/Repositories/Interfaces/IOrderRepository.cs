using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface IOrderRepository
{
    Task<Order?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Order?> FindTrackedByIdAsync(string id, CancellationToken ct = default);
    Task<Order?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default);
    Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<PaginatedResult<Order>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<Order>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
