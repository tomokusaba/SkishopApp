namespace SalesManagementService.DTOs.Responses;

public record SalesReportDto(
    DateTimeOffset FromDate, DateTimeOffset ToDate,
    int TotalOrders, decimal TotalRevenue, decimal TotalTax,
    decimal TotalShippingFee, decimal AverageOrderValue,
    IReadOnlyList<DailySalesDto> DailySales);

public record DailySalesDto(DateOnly Date, int OrderCount, decimal Revenue);
