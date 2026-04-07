using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class ReturnRepository(SalesDbContext context) : IReturnRepository
{
    public async Task<Return?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Returns
            .AsNoTracking()
            .Include(r => r.OrderItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Return?> FindTrackedByIdAsync(string id, CancellationToken ct = default)
        => await context.Returns
            .Include(r => r.OrderItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Return?> FindByReturnNumberAsync(string returnNumber, CancellationToken ct = default)
        => await context.Returns
            .AsNoTracking()
            .Include(r => r.OrderItem)
            .FirstOrDefaultAsync(r => r.ReturnNumber == returnNumber, ct);

    public async Task<PaginatedResult<Return>> FindByOrderIdAsync(
        string orderId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Returns
            .AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.RequestedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Return>(items, totalCount, page, pageSize);
    }

    public async Task<PaginatedResult<Return>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Returns
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RequestedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Return>(items, totalCount, page, pageSize);
    }

    public async Task<PaginatedResult<Return>> FindByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Returns
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.RequestedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Return>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(Return returnEntity, CancellationToken ct = default)
        => await context.Returns.AddAsync(returnEntity, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
