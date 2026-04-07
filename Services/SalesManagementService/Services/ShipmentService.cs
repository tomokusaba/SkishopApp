using Microsoft.EntityFrameworkCore;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Models;
using SalesManagementService.Repositories;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Services;

public class ShipmentService(
    IShipmentRepository shipmentRepository,
    IOrderRepository orderRepository,
    TimeProvider timeProvider,
    ILogger<ShipmentService> logger) : IShipmentService
{
    private static readonly Dictionary<string, HashSet<string>> AllowedShipmentTransitions = new()
    {
        ["PREPARING"] = ["SHIPPED"],
        ["SHIPPED"] = ["IN_TRANSIT", "DELIVERED"],
        ["IN_TRANSIT"] = ["DELIVERED", "FAILED"],
        ["DELIVERED"] = [],
        ["FAILED"] = ["PREPARING"]
    };

    public async Task<ShipmentDto> CreateShipmentAsync(ShipmentCreateRequest request, CancellationToken ct = default)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {request.OrderId}");

        var shipment = new Shipment
        {
            OrderId = request.OrderId,
            Carrier = request.Carrier,
            TrackingNumber = request.TrackingNumber,
            ShippingRecipientName = order.ShippingRecipientName,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingPrefecture = order.ShippingPrefecture,
            ShippingCity = order.ShippingCity,
            ShippingAddressLine1 = order.ShippingAddressLine1,
            ShippingAddressLine2 = order.ShippingAddressLine2,
            ShippingPhoneNumber = order.ShippingPhoneNumber
        };

        await shipmentRepository.AddAsync(shipment, ct);
        await SaveChangesWithConcurrencyHandlingAsync(ct);

        logger.LogInformation("出荷作成完了: ShipmentId={ShipmentId}, OrderId={OrderId}", shipment.Id, request.OrderId);

        return MapToDto(shipment);
    }

    public async Task<ShipmentDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var shipment = await shipmentRepository.FindByIdAsync(id, ct);
        return shipment is null ? null : MapToDto(shipment);
    }

    public async Task<ShipmentDto?> GetByOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        var shipment = await shipmentRepository.FindByOrderIdAsync(orderId, ct);
        return shipment is null ? null : MapToDto(shipment);
    }

    public async Task<PaginatedResult<ShipmentDto>> GetByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await shipmentRepository.FindByStatusAsync(status, page, pageSize, ct);
        return new PaginatedResult<ShipmentDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task UpdateShipmentAsync(string id, ShipmentUpdateRequest request, CancellationToken ct = default)
    {
        var shipment = await shipmentRepository.FindTrackedByIdAsync(id, ct)
            ?? throw new NotFoundException($"出荷が見つかりません: {id}");

        ValidateShipmentTransition(shipment.Status, request.Status);

        shipment.Status = request.Status;

        if (request.TrackingNumber is not null)
            shipment.TrackingNumber = request.TrackingNumber;

        if (request.Status == "SHIPPED")
            shipment.ShippedAt = timeProvider.GetUtcNow();
        else if (request.Status == "DELIVERED")
            shipment.DeliveredAt = timeProvider.GetUtcNow();

        await SaveChangesWithConcurrencyHandlingAsync(ct);
        logger.LogInformation("出荷ステータス更新: ShipmentId={ShipmentId}, NewStatus={NewStatus}", id, request.Status);
    }

    private static void ValidateShipmentTransition(string currentStatus, string newStatus)
    {
        if (!AllowedShipmentTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOrderStateException(
                $"出荷ステータスを '{currentStatus}' から '{newStatus}' に変更できません");
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync(CancellationToken ct)
    {
        try
        {
            await shipmentRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合が発生しました");
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
        }
    }

    private static ShipmentDto MapToDto(Shipment shipment) => new(
        shipment.Id, shipment.OrderId, shipment.Carrier, shipment.TrackingNumber,
        shipment.Status, shipment.ShippedAt, shipment.EstimatedDeliveryAt,
        shipment.DeliveredAt, shipment.CreatedAt);
}
