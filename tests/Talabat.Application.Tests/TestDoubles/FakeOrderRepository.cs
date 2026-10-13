using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeOrderRepository : IOrderRepository
{
    public List<Order> Orders { get; } = [];

    public int AddCount { get; private set; }

    public Task<Order?> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.SingleOrDefault(order => order.Id == orderId));
    }

    public Task<Order?> GetByIdForCustomerAsync(
        int orderId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Orders.SingleOrDefault(order =>
                order.Id == orderId &&
                order.CustomerId == customerId));
    }

    public Task<IReadOnlyCollection<Order>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Order> orders = Orders
            .Where(order => order.CustomerId == customerId)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(orders);
    }

    public Task<(IReadOnlyCollection<Order> Items, int TotalCount)> GetPageByCustomerIdAsync(
        int customerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var customerOrders = Orders
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .ToList();

        IReadOnlyCollection<Order> page = customerOrders.Skip(skip).Take(take).ToList().AsReadOnly();
        return Task.FromResult((page, customerOrders.Count));
    }

    public Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AddCount++;
        Orders.Add(order);
        return Task.CompletedTask;
    }
}
