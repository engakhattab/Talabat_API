using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly object[] _stores;

    public FakeUnitOfWork(params object[] stores)
    {
        _stores = stores;
    }

    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignIds();
        SaveChangesCount++;
        return Task.FromResult(SaveChangesCount);
    }

    public int TransactionCount { get; private set; }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        await operation(cancellationToken);
    }

    private void AssignIds()
    {
        foreach (var store in _stores)
        {
            switch (store)
            {
                case FakeCartRepository carts:
                    AssignCartIds(carts);
                    break;
                case FakeUserRepository users:
                    AssignUserIds(users);
                    break;
                case FakeOrderRepository orders:
                    AssignOrderIds(orders);
                    break;
                case FakeDeliveryRepository deliveries:
                    AssignDeliveryIds(deliveries);
                    break;
                case FakeOrderFulfillmentRepository fulfillments:
                    AssignFulfillmentIds(fulfillments);
                    break;
            }
        }
    }

    private static void AssignCartIds(FakeCartRepository carts)
    {
        foreach (var cart in carts.Carts.Where(cart => cart.Id == 0))
        {
            TestIds.SetId(
                cart,
                NextId(carts.Carts.Select(existingCart => existingCart.Id), seed: 100));
        }
    }

    private static void AssignUserIds(FakeUserRepository users)
    {
        foreach (var user in users.Users.Where(user => user.Id == 0))
        {
            TestIds.SetId(
                user,
                NextId(users.Users.Select(existingUser => existingUser.Id), seed: 1));
        }
    }

    private static void AssignOrderIds(FakeOrderRepository orders)
    {
        foreach (var order in orders.Orders.Where(order => order.Id == 0))
        {
            TestIds.SetId(
                order,
                NextId(orders.Orders.Select(existingOrder => existingOrder.Id), seed: 300));
        }
    }

    private static void AssignDeliveryIds(FakeDeliveryRepository deliveries)
    {
        foreach (var delivery in deliveries.Deliveries.Where(delivery => delivery.Id == 0))
        {
            TestIds.SetId(
                delivery,
                NextId(deliveries.Deliveries.Select(existing => existing.Id), seed: 500));
        }
    }

    private static void AssignFulfillmentIds(FakeOrderFulfillmentRepository fulfillments)
    {
        foreach (var fulfillment in fulfillments.Fulfillments.Where(fulfillment => fulfillment.Id == 0))
        {
            TestIds.SetId(
                fulfillment,
                NextId(fulfillments.Fulfillments.Select(existing => existing.Id), seed: 700));
        }
    }

    private static int NextId(IEnumerable<int> existingIds, int seed)
    {
        return Math.Max(seed - 1, existingIds.DefaultIfEmpty(0).Max()) + 1;
    }
}
