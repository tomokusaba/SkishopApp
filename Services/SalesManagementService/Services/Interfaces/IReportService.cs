using SalesManagementService.DTOs.Responses;

namespace SalesManagementService.Services.Interfaces;

public interface IReportService
{
    Task<SalesReportDto> GetSalesReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
