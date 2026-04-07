namespace SalesManagementService.DTOs.Responses;

public record ShipmentDto(
    string Id, string OrderId, string Carrier, string? TrackingNumber,
    string Status, DateTimeOffset? ShippedAt, DateTimeOffset? EstimatedDeliveryAt,
    DateTimeOffset? DeliveredAt, DateTimeOffset CreatedAt);
