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
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<OrderFulfillment>> GetByOrderIdsAsync(
        IReadOnlyCollection<int> orderIds,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(
        OrderFulfillment fulfillment,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
