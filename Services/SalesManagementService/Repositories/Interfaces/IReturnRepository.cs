using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface IReturnRepository
{
    Task<Return?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Return?> FindTrackedByIdAsync(string id, CancellationToken ct = default);
    Task<Return?> FindByReturnNumberAsync(string returnNumber, CancellationToken ct = default);
    Task<PaginatedResult<Return>> FindByOrderIdAsync(
        string orderId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<Return>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<Return>> FindByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Return returnEntity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
