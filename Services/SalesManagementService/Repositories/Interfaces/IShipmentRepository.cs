using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface IShipmentRepository
{
    Task<Shipment?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Shipment?> FindTrackedByIdAsync(string id, CancellationToken ct = default);
    Task<Shipment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default);
    Task<PaginatedResult<Shipment>> FindByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Shipment shipment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
