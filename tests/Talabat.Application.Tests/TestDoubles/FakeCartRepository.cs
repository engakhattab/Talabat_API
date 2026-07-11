using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeCartRepository : ICartRepository
{
    public List<Cart> Carts { get; } = [];

    public Cart? CartToReturn { get; set; }

    public int AddCount { get; private set; }

    public int UpdateCount { get; private set; }

    public Task<Cart?> GetActiveCartByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (CartToReturn?.CustomerId == customerId)
        {
            return Task.FromResult<Cart?>(CartToReturn);
        }

        return Task.FromResult(
            Carts.LastOrDefault(cart =>
                cart.CustomerId == customerId &&
                cart.Status == CartStatus.Active));
    }

    public Task AddAsync(
        Cart cart,
        CancellationToken cancellationToken = default)
    {
        AddCount++;
        Carts.Add(cart);
        return Task.CompletedTask;
    }

    public void Update(Cart cart)
    {
        UpdateCount++;

        if (!Carts.Contains(cart))
        {
            Carts.Add(cart);
        }
    }
}
