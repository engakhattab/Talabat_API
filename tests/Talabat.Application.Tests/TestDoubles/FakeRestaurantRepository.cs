using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeRestaurantRepository : IRestaurantRepository
{
    public List<Restaurant> Restaurants { get; } = [];

    public Task<IReadOnlyCollection<Restaurant>> GetActiveRestaurantsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Restaurant> restaurants = Restaurants
            .Where(restaurant => restaurant.IsActive)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(restaurants);
    }

    public Task<Restaurant?> GetByIdAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Restaurants.SingleOrDefault(restaurant => restaurant.Id == restaurantId));
    }

    public Task<Restaurant?> GetByIdWithProductsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(restaurantId, cancellationToken);
    }

    public Task<CatalogProductSnapshot?> GetProductSnapshotAsync(
        int restaurantId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var restaurant = Restaurants.SingleOrDefault(item => item.Id == restaurantId);
        var product = restaurant?.FindProduct(productId);

        CatalogProductSnapshot? snapshot = product is null
            ? null
            : new CatalogProductSnapshot(
                product.Id,
                product.RestaurantId,
                product.Name,
                product.IsAvailable);

        return Task.FromResult(snapshot);
    }

    public Task<bool> ExistsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Restaurants.Any(restaurant => restaurant.Id == restaurantId));
    }
}
