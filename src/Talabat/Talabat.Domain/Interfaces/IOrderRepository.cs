using Talabat.Domain.Aggregates.Ordering;

namespace Talabat.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForCustomerAsync(
        int orderId,
        int customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default);
}
