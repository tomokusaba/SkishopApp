using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Repositories;

namespace SalesManagementService.Services.Interfaces;

public interface IShipmentService
{
    Task<ShipmentDto> CreateShipmentAsync(ShipmentCreateRequest request, CancellationToken ct = default);
    Task<ShipmentDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ShipmentDto?> GetByOrderIdAsync(string orderId, CancellationToken ct = default);
    Task<PaginatedResult<ShipmentDto>> GetByStatusAsync(string status, int page, int pageSize, CancellationToken ct = default);
    Task UpdateShipmentAsync(string id, ShipmentUpdateRequest request, CancellationToken ct = default);
}
