using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class ReportQueryRepository(SalesDbContext context) : IReportQueryRepository
{
    public async Task<SalesReportSnapshot> GetSalesReportSnapshotAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var baseQuery = context.Orders
            .AsNoTracking()
            .Where(o => o.OrderDate >= from && o.OrderDate <= to && o.Status != "CANCELLED");

        var totalOrders = await baseQuery.CountAsync(ct);
        var totalRevenue = await baseQuery.SumAsync(o => o.TotalAmount, ct);
        var totalTax = await baseQuery.SumAsync(o => o.TaxAmount, ct);
        var totalShippingFee = await baseQuery.SumAsync(o => o.ShippingFee, ct);

        // PostgreSQLではGroupBy(o => o.OrderDate.Date)がサーバー側で変換できないため
        // クライアント評価で処理する
        var ordersForDailyGroup = await baseQuery
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync(ct);

        var dailySales = ordersForDailyGroup
            .GroupBy(o => DateOnly.FromDateTime(o.OrderDate.DateTime))
            .Select(g => new DailySalesSnapshot(
                g.Key,
                g.Count(),
                g.Sum(o => o.TotalAmount)))
            .OrderBy(d => d.Date)
            .ToList();

        return new SalesReportSnapshot(
            totalOrders,
            totalRevenue,
            totalTax,
            totalShippingFee,
            dailySales);
    }
}
