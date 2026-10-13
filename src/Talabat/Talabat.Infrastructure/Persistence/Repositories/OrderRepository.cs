using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly TalabatDbContext _dbContext;

    public OrderRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Order?> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders
            .Include("_items")
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public Task<Order?> GetByIdForCustomerAsync(
        int orderId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders
            .Include("_items")
            .SingleOrDefaultAsync(
                order => order.Id == orderId && order.CustomerId == customerId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include("_items")
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Order> Items, int TotalCount)> GetPageByCustomerIdAsync(
        int customerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include("_items")
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }
}
