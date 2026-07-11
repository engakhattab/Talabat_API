using Talabat.Domain.Aggregates.Basket;

namespace Talabat.Domain.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetActiveCartByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Cart cart,
        CancellationToken cancellationToken = default);

    void Update(Cart cart);
}
