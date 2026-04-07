using Microsoft.AspNetCore.Mvc;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization("AdminOnly")
            ;

        group.MapGet("/sales", GetSalesReport).WithName("GetSalesReport");
    }

    private static async Task<IResult> GetSalesReport(
        [FromQuery] DateTimeOffset fromDate,
        [FromQuery] DateTimeOffset toDate,
        IReportService reportService,
        CancellationToken ct = default)
    {
        var today = DateTimeOffset.UtcNow;
        if (fromDate > toDate)
            return TypedResults.Problem("fromDate は toDate 以下である必要があります", statusCode: 400);
        if (toDate > today)
            return TypedResults.Problem("toDate に未来日は指定できません", statusCode: 400);
        if ((toDate - fromDate).TotalDays > 366)
            return TypedResults.Problem("集計期間は 366 日以内で指定してください", statusCode: 400);

        var report = await reportService.GetSalesReportAsync(fromDate, toDate, ct);
        return Results.Ok(report);
    }
}
