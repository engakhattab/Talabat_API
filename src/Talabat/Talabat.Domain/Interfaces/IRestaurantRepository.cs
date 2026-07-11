using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Interfaces;

public interface IRestaurantRepository
{
    Task<IReadOnlyCollection<Restaurant>> GetActiveRestaurantsAsync(
        CancellationToken cancellationToken = default);

    Task<Restaurant?> GetByIdAsync(
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task<Restaurant?> GetByIdWithProductsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task<CatalogProductSnapshot?> GetProductSnapshotAsync(
        int restaurantId,
        int productId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        int restaurantId,
        CancellationToken cancellationToken = default);
}
