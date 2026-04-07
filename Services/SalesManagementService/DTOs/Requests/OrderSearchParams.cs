namespace SalesManagementService.DTOs.Requests;

public record OrderSearchParams(
    string? CustomerId = null,
    string? Status = null,
    string? PaymentStatus = null,
    int Page = 1,
    int PageSize = 20);
