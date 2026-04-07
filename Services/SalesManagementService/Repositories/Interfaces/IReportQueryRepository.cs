namespace SalesManagementService.Repositories.Interfaces;

public interface IReportQueryRepository
{
    Task<SalesReportSnapshot> GetSalesReportSnapshotAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
