using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class RestaurantRepository : IRestaurantRepository
{
    private readonly TalabatDbContext _dbContext;

    public RestaurantRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyCollection<Restaurant>> GetActiveRestaurantsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.IsActive)
            .OrderBy(restaurant => restaurant.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Restaurant?> GetByIdAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                restaurant => restaurant.Id == restaurantId,
                cancellationToken);
    }

    public Task<Restaurant?> GetByIdWithProductsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Restaurants
            .Include("_products")
            .SingleOrDefaultAsync(
                restaurant => restaurant.Id == restaurantId,
                cancellationToken);
    }

    public async Task<CatalogProductSnapshot?> GetProductSnapshotAsync(
        int restaurantId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.RestaurantId == restaurantId && product.Id == productId)
            .Select(product => new CatalogProductSnapshot(
                product.Id,
                product.RestaurantId,
                product.Name,
                product.IsAvailable))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Restaurants
            .AsNoTracking()
            .AnyAsync(restaurant => restaurant.Id == restaurantId, cancellationToken);
    }
}
