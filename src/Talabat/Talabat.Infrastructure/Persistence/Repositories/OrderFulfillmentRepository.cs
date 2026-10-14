using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class OrderFulfillmentRepository : IOrderFulfillmentRepository
{
    private readonly TalabatDbContext _dbContext;

    public OrderFulfillmentRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<OrderFulfillment?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.OrderFulfillments
            .SingleOrDefaultAsync(fulfillment => fulfillment.OrderId == orderId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderFulfillment>> GetByOrderIdsAsync(
        IReadOnlyCollection<int> orderIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);

        return await _dbContext.OrderFulfillments
            .AsNoTracking()
            .Where(fulfillment => orderIds.Contains(fulfillment.OrderId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        OrderFulfillment fulfillment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fulfillment);
        await _dbContext.OrderFulfillments.AddAsync(fulfillment, cancellationToken);
    }
}
