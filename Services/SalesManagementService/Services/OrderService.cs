using Microsoft.EntityFrameworkCore;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.Saga;
using SalesManagementService.Models;
using SalesManagementService.Repositories;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Services;

public class OrderService(
    IOrderRepository orderRepository,
    OrderNumberGenerator orderNumberGenerator,
    ShippingFeeCalculator shippingFeeCalculator,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderDetailDto> CreateOrderAsync(
        OrderCreateRequest request, SagaContext sagaContext, CancellationToken ct = default)
    {
        var orderNumber = orderNumberGenerator.GenerateOrderNumber();
        logger.LogInformation("注文作成開始: OrderNumber={OrderNumber}", orderNumber);

        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerId = request.CustomerId,
            PaymentMethod = request.PaymentMethod,
            CouponCode = request.CouponCode,
            UsedPoints = request.UsedPoints,
            Notes = request.Notes,
            ShippingRecipientName = request.ShippingAddress.RecipientName,
            ShippingPostalCode = request.ShippingAddress.PostalCode,
            ShippingPrefecture = request.ShippingAddress.Prefecture,
            ShippingCity = request.ShippingAddress.City,
            ShippingAddressLine1 = request.ShippingAddress.AddressLine1,
            ShippingAddressLine2 = request.ShippingAddress.AddressLine2,
            ShippingPhoneNumber = request.ShippingAddress.PhoneNumber,
            CreatedBy = sagaContext.UserId,
            UpdatedBy = sagaContext.UserId
        };

        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity);
        }

        var subtotal = order.Items.Sum(i => i.Subtotal);
        order.SubtotalAmount = subtotal;
        order.TaxAmount = TaxCalculator.CalculateStandardTax(subtotal);
        order.ShippingFee = shippingFeeCalculator.Calculate(subtotal);
        order.TotalAmount = subtotal + order.TaxAmount + order.ShippingFee - order.DiscountAmount;

        await orderRepository.AddAsync(order, ct);
        await SaveChangesWithConcurrencyHandlingAsync(ct);

        logger.LogInformation("注文作成完了: OrderId={OrderId}, OrderNumber={OrderNumber}", order.Id, orderNumber);

        return MapToDetailDto(order);
    }

    public async Task<OrderDetailDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var order = await orderRepository.FindByIdWithDetailsAsync(id, ct);
        return order is null ? null : MapToDetailDto(order);
    }

    public async Task<OrderDetailDto?> GetByIdAndUserIdAsync(string id, string userId, CancellationToken ct = default)
    {
        var order = await orderRepository.FindByIdWithDetailsAsync(id, ct);
        if (order is null)
            return null;

        if (order.CustomerId != userId)
            throw new ForbiddenException();

        return MapToDetailDto(order);
    }

    public async Task<OrderDetailDto?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        var order = await orderRepository.FindByOrderNumberAsync(orderNumber, ct);
        return order is null ? null : MapToDetailDto(order);
    }

    public async Task<PaginatedResult<OrderDto>> GetByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await orderRepository.FindByCustomerIdAsync(customerId, page, pageSize, ct);
        return new PaginatedResult<OrderDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<PaginatedResult<OrderDto>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await orderRepository.SearchAsync(customerId, status, paymentStatus, page, pageSize, ct);
        return new PaginatedResult<OrderDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task CancelOrderAsync(string orderId, string reason, CancellationToken ct = default)
    {
        var order = await orderRepository.FindTrackedByIdAsync(orderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {orderId}");

        OrderStateMachine.ValidateTransition(order.Status, "CANCELLED");
        order.Status = "CANCELLED";
        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
            ? $"キャンセル理由: {reason}"
            : $"{order.Notes}\nキャンセル理由: {reason}";

        await SaveChangesWithConcurrencyHandlingAsync(ct);
        logger.LogInformation("注文キャンセル完了: OrderId={OrderId}", orderId);
    }

    public async Task UpdateStatusAsync(string orderId, string newStatus, CancellationToken ct = default)
    {
        var order = await orderRepository.FindTrackedByIdAsync(orderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {orderId}");

        OrderStateMachine.ValidateTransition(order.Status, newStatus);
        order.Status = newStatus;

        await SaveChangesWithConcurrencyHandlingAsync(ct);
        logger.LogInformation("注文ステータス更新: OrderId={OrderId}, NewStatus={NewStatus}", orderId, newStatus);
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync(CancellationToken ct)
    {
        try
        {
            await orderRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合が発生しました");
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
        }
    }

    private static OrderDetailDto MapToDetailDto(Order order) => new(
        order.Id, order.OrderNumber, order.CustomerId,
        order.OrderDate, order.Status, order.PaymentStatus,
        order.PaymentMethod, order.SubtotalAmount, order.TaxAmount,
        order.ShippingFee, order.DiscountAmount, order.TotalAmount,
        order.CouponCode, order.UsedPoints, order.PointDiscountAmount,
        order.ShippingPostalCode, order.ShippingPrefecture,
        order.ShippingCity, order.ShippingAddressLine1,
        order.ShippingAddressLine2, order.ShippingRecipientName,
        order.ShippingPhoneNumber, order.CurrencyCode,
        order.Notes, order.CreatedAt, order.UpdatedAt,
        order.Items.Select(i => new OrderItemDto(
            i.Id, i.ProductId, i.ProductName, i.Sku,
            i.UnitPrice, i.Quantity, i.Subtotal)).ToList());

    private static OrderDto MapToDto(Order order) => new(
        order.Id, order.OrderNumber, order.CustomerId,
        order.OrderDate, order.Status, order.PaymentStatus,
        order.SubtotalAmount, order.TaxAmount, order.ShippingFee,
        order.DiscountAmount, order.TotalAmount,
        order.CurrencyCode, order.CreatedAt);
}
