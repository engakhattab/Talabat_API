using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Domain.Interfaces;

public interface IOrderFulfillmentRepository
{
    Task<OrderFulfillment?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderFulfillment>> GetByOrderIdsAsync(
        IReadOnlyCollection<int> orderIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        OrderFulfillment fulfillment,
        CancellationToken cancellationToken = default);
}
