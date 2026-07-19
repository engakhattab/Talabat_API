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

    private static int NextId(IEnumerable<int> existingIds, int seed)
    {
        return Math.Max(seed - 1, existingIds.DefaultIfEmpty(0).Max()) + 1;
    }
}
