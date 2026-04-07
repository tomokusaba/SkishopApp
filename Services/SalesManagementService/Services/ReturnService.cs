using Microsoft.EntityFrameworkCore;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Models;
using SalesManagementService.Repositories;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Services;

public class ReturnService(
    IReturnRepository returnRepository,
    IOrderRepository orderRepository,
    OrderNumberGenerator orderNumberGenerator,
    TimeProvider timeProvider,
    ILogger<ReturnService> logger) : IReturnService
{
    public async Task<ReturnDto> CreateReturnAsync(
        ReturnCreateRequest request, string customerId, CancellationToken ct = default)
    {
        var order = await orderRepository.FindByIdWithDetailsAsync(request.OrderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {request.OrderId}");

        if (order.CustomerId != customerId)
            throw new ForbiddenException();

        var orderItem = order.Items.FirstOrDefault(i => i.Id == request.OrderItemId)
            ?? throw new NotFoundException($"注文商品が見つかりません: {request.OrderItemId}");

        if (request.Quantity > orderItem.Quantity)
            throw new BusinessException($"返品数量が注文数量を超えています（注文数量: {orderItem.Quantity}）");

        var returnNumber = orderNumberGenerator.GenerateReturnNumber();
        var now = timeProvider.GetUtcNow();
        var refundAmount = orderItem.UnitPrice * request.Quantity;

        var returnEntity = new Return
        {
            ReturnNumber = returnNumber,
            OrderId = request.OrderId,
            OrderItemId = request.OrderItemId,
            CustomerId = customerId,
            Reason = request.Reason,
            ReasonDetail = request.ReasonDetail,
            Quantity = request.Quantity,
            RefundAmount = refundAmount,
            RequestedAt = now,
            CreatedBy = customerId,
            UpdatedBy = customerId
        };

        await returnRepository.AddAsync(returnEntity, ct);
        await SaveChangesWithConcurrencyHandlingAsync(ct);

        logger.LogInformation("返品申請作成完了: ReturnId={ReturnId}, ReturnNumber={ReturnNumber}, OrderId={OrderId}",
            returnEntity.Id, returnNumber, request.OrderId);

        return MapToDto(returnEntity);
    }

    public async Task<ReturnDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var returnEntity = await returnRepository.FindByIdAsync(id, ct);
        return returnEntity is null ? null : MapToDto(returnEntity);
    }

    public async Task<PaginatedResult<ReturnDto>> GetByOrderIdAsync(
        string orderId, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await returnRepository.FindByOrderIdAsync(orderId, page, pageSize, ct);
        return new PaginatedResult<ReturnDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<PaginatedResult<ReturnDto>> GetByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await returnRepository.FindByCustomerIdAsync(customerId, page, pageSize, ct);
        return new PaginatedResult<ReturnDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<PaginatedResult<ReturnDto>> GetByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await returnRepository.FindByStatusAsync(status, page, pageSize, ct);
        return new PaginatedResult<ReturnDto>(
            result.Items.Select(MapToDto).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task ProcessReturnAsync(string id, ReturnProcessRequest request, CancellationToken ct = default)
    {
        var returnEntity = await returnRepository.FindTrackedByIdAsync(id, ct)
            ?? throw new NotFoundException($"返品が見つかりません: {id}");

        ValidateReturnStatusTransition(returnEntity.Status, request.Status);

        var now = timeProvider.GetUtcNow();
        returnEntity.Status = request.Status;

        if (request.AdminNotes is not null)
            returnEntity.AdminNotes = request.AdminNotes;

        switch (request.Status)
        {
            case "APPROVED":
                returnEntity.ApprovedAt = now;
                break;
            case "RECEIVED":
                returnEntity.ReceivedAt = now;
                break;
            case "REFUNDED":
                returnEntity.RefundedAt = now;
                break;
        }

        await SaveChangesWithConcurrencyHandlingAsync(ct);
        logger.LogInformation("返品処理完了: ReturnId={ReturnId}, NewStatus={NewStatus}", id, request.Status);
    }

    private static readonly Dictionary<string, HashSet<string>> AllowedReturnTransitions = new()
    {
        ["REQUESTED"] = ["APPROVED", "REJECTED"],
        ["APPROVED"] = ["RECEIVED", "CLOSED"],
        ["REJECTED"] = ["CLOSED"],
        ["RECEIVED"] = ["REFUNDED", "CLOSED"],
        ["REFUNDED"] = ["CLOSED"],
        ["CLOSED"] = []
    };

    private static void ValidateReturnStatusTransition(string currentStatus, string newStatus)
    {
        if (!AllowedReturnTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOrderStateException(
                $"返品ステータスを '{currentStatus}' から '{newStatus}' に変更できません");
    }

    private async Task SaveChangesWithConcurrencyHandlingAsync(CancellationToken ct)
    {
        try
        {
            await returnRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合が発生しました");
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
        }
    }

    private static ReturnDto MapToDto(Return r) => new(
        r.Id, r.ReturnNumber, r.OrderId, r.OrderItemId,
        r.CustomerId, r.Reason, r.ReasonDetail,
        r.Quantity, r.RefundAmount, r.Status,
        r.RequestedAt, r.ApprovedAt,
        r.ReceivedAt, r.RefundedAt,
        r.CreatedAt);
}
