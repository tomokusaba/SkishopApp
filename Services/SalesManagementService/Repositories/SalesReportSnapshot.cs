namespace SalesManagementService.Repositories;

public sealed record SalesReportSnapshot(
    int TotalOrders,
    decimal TotalRevenue,
    decimal TotalTax,
    decimal TotalShippingFee,
    IReadOnlyList<DailySalesSnapshot> DailySales);

public sealed record DailySalesSnapshot(
    DateOnly Date,
    int OrderCount,
    decimal Revenue);
