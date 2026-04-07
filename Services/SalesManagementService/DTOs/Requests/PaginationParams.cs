using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record PaginationParams(
    [Range(1, int.MaxValue)] int Page = 1,
    [Range(1, 100)] int PageSize = 20);
