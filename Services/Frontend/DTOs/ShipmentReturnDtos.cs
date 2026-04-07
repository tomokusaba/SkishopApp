namespace Frontend.DTOs;

public record ShipmentDto(
    string Id, string OrderId, string Status, string? TrackingNumber,
    string? CarrierName, string? CarrierTrackingUrl, DateTime? EstimatedDeliveryDate,
    DateTime CreatedAt, List<ShipmentEventDto> Events);
public record ShipmentEventDto(string Status, string Description, DateTime OccurredAt);

public record ReturnDto(
    string Id, string OrderId, string Status,
    List<ReturnItemDto> Items, string? Description, DateTime RequestedAt);
public record ReturnItemDto(string OrderItemId, string ProductName, int Quantity, string Reason);

public record CreateReturnRequest(string OrderId, List<CreateReturnItemRequest> Items, string? Description);
public record CreateReturnItemRequest(string OrderItemId, int Quantity, string Reason);
