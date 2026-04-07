using SalesManagementService.DTOs.Responses;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Services;

public class ReportService(
    IReportQueryRepository reportQueryRepository,
    ILogger<ReportService> logger) : IReportService
{
    public async Task<SalesReportDto> GetSalesReportAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        logger.LogInformation("売上レポート生成開始: From={From}, To={To}", from, to);

        var snapshot = await reportQueryRepository.GetSalesReportSnapshotAsync(from, to, ct);
        var totalOrders = snapshot.TotalOrders;
        var totalRevenue = snapshot.TotalRevenue;
        var totalTax = snapshot.TotalTax;
        var totalShippingFee = snapshot.TotalShippingFee;
        var averageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;
        var dailySales = snapshot.DailySales
            .Select(d => new DailySalesDto(d.Date, d.OrderCount, d.Revenue))
            .ToList();

        logger.LogInformation("売上レポート生成完了: TotalOrders={TotalOrders}, TotalRevenue={TotalRevenue}",
            totalOrders, totalRevenue);

        return new SalesReportDto(
            from, to, totalOrders, totalRevenue, totalTax,
            totalShippingFee, averageOrderValue, dailySales);
    }
}
