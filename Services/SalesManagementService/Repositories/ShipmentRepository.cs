using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class ShipmentRepository(SalesDbContext context) : IShipmentRepository
{
    public async Task<Shipment?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Shipment?> FindTrackedByIdAsync(string id, CancellationToken ct = default)
        => await context.Shipments
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Shipment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default)
        => await context.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task<PaginatedResult<Shipment>> FindByStatusAsync(
        string status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Shipments
            .AsNoTracking()
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Shipment>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(Shipment shipment, CancellationToken ct = default)
        => await context.Shipments.AddAsync(shipment, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
