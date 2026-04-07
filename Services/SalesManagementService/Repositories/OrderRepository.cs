using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class OrderRepository(SalesDbContext context) : IOrderRepository
{
    public async Task<Order?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> FindTrackedByIdAsync(string id, CancellationToken ct = default)
        => await context.Orders
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default)
        => await context.Orders
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .Include(o => o.Returns)
            .Include(o => o.Invoice)
            .Include(o => o.SagaLog)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
        => await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public async Task<PaginatedResult<Order>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Order>(items, totalCount, page, pageSize);
    }

    public async Task<PaginatedResult<Order>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(paymentStatus))
            query = query.Where(o => o.PaymentStatus == paymentStatus);

        query = query.OrderByDescending(o => o.OrderDate);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Order>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await context.Orders.AddAsync(order, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
