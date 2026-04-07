namespace SalesManagementService.DTOs.Responses;

public record ReturnDto(
    string Id, string ReturnNumber, string OrderId, string OrderItemId,
    string CustomerId, string Reason, string? ReasonDetail,
    int Quantity, decimal RefundAmount, string Status,
    DateTimeOffset RequestedAt, DateTimeOffset? ApprovedAt,
    DateTimeOffset? ReceivedAt, DateTimeOffset? RefundedAt,
    DateTimeOffset CreatedAt);
