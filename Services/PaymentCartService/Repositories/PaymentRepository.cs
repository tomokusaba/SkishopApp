using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task<Payment?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> FindByIdWithTransactionsAsync(string id, CancellationToken ct = default)
        => await context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task<Payment?> FindByOrderIdWithTransactionsAsync(string orderId, CancellationToken ct = default)
        => await context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task<Payment?> FindByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == sessionId, ct);

    public async Task<Payment?> FindByStripePaymentIntentIdAsync(string intentId, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == intentId, ct);

    public async Task<(List<Payment> Items, int TotalCount)> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Payments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
        => await context.Payments.AddAsync(payment, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
