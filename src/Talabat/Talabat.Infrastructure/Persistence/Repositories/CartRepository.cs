using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class CartRepository : ICartRepository
{
    private readonly TalabatDbContext _dbContext;

    public CartRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Cart?> GetActiveCartByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Carts
            .Include("_items")
            .SingleOrDefaultAsync(
                cart => cart.CustomerId == customerId && cart.Status == CartStatus.Active,
                cancellationToken);
    }

    public async Task AddAsync(
        Cart cart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        await _dbContext.Carts.AddAsync(cart, cancellationToken);
    }

    public void Update(Cart cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        _dbContext.Carts.Update(cart);
    }
}
