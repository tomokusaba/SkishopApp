using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Repositories;

namespace SalesManagementService.Services.Interfaces;

public interface IReturnService
{
    Task<ReturnDto> CreateReturnAsync(ReturnCreateRequest request, string customerId, CancellationToken ct = default);
    Task<ReturnDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PaginatedResult<ReturnDto>> GetByOrderIdAsync(string orderId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<ReturnDto>> GetByCustomerIdAsync(string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<ReturnDto>> GetByStatusAsync(string status, int page, int pageSize, CancellationToken ct = default);
    Task ProcessReturnAsync(string id, ReturnProcessRequest request, CancellationToken ct = default);
}
