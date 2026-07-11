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
                case FakeCustomerRepository customers:
                    AssignCustomerIds(customers);
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

    private static void AssignCustomerIds(FakeCustomerRepository customers)
    {
        foreach (var customer in customers.Customers.Where(customer => customer.Id == 0))
        {
            TestIds.SetId(
                customer,
                NextId(customers.Customers.Select(existingCustomer => existingCustomer.Id), seed: 1));
        }

        var addresses = customers.Customers
            .SelectMany(customer => customer.Addresses)
            .ToList();

        foreach (var address in addresses.Where(address => address.Id == 0))
        {
            TestIds.SetId(
                address,
                NextId(addresses.Select(existingAddress => existingAddress.Id), seed: 200));
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
