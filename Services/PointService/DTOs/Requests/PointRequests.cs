using System.ComponentModel.DataAnnotations;

namespace PointService.DTOs.Requests;

public record ReservePointsRequest(
    [Required] string UserId,
    [Required] string OrderId,
    [Required, Range(1, int.MaxValue)] int Points);

public record ConfirmPointsRequest(
    [Required] string UserId,
    [Required] string OrderId);

public record ReleasePointsRequest(
    [Required] string UserId,
    [Required] string OrderId);

public record AwardPointsRequest(
    [Required] string UserId,
    [Required] string OrderId,
    [Required, Range(0.01, double.MaxValue)] decimal OrderAmount);

public record AdjustPointsRequest(
    [Required, Range(1, int.MaxValue)] int Points,
    [Required] string Reason,
    string? ReferenceId = null);

public record PaginationQuery(
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);

public record UpdateTierRequest(
    [Required, Range(0.001, 1.0)] decimal PointRate,
    [Required, Range(0, int.MaxValue)] int MinAnnualPoints,
    string? Benefits = null);

public record CreateAccountRequest(
    [Required] string UserId);
