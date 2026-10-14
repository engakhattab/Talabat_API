using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeOrderFulfillmentRepository : IOrderFulfillmentRepository
{
    public List<OrderFulfillment> Fulfillments { get; } = [];

    public Task<OrderFulfillment?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Fulfillments.SingleOrDefault(fulfillment => fulfillment.OrderId == orderId));
    }

    public Task<IReadOnlyCollection<OrderFulfillment>> GetByOrderIdsAsync(
        IReadOnlyCollection<int> orderIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<OrderFulfillment> matches = Fulfillments
            .Where(fulfillment => orderIds.Contains(fulfillment.OrderId))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task AddAsync(
        OrderFulfillment fulfillment,
        CancellationToken cancellationToken = default)
    {
        Fulfillments.Add(fulfillment);
        return Task.CompletedTask;
    }
}
