using Talabat.Application.Abstractions;
using Talabat.Application.Catalog.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Catalog.BrowseRestaurants;

public sealed class BrowseRestaurantsHandler
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantLocalTimeProvider _restaurantLocalTimeProvider;
    private readonly IClock _clock;

    public BrowseRestaurantsHandler(
        IRestaurantRepository restaurantRepository,
        IRestaurantLocalTimeProvider restaurantLocalTimeProvider,
        IClock clock)
    {
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _restaurantLocalTimeProvider = restaurantLocalTimeProvider ?? throw new ArgumentNullException(nameof(restaurantLocalTimeProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<RestaurantSummary>>> Handle(
        BrowseRestaurantsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var restaurants = await _restaurantRepository.GetActiveRestaurantsAsync(cancellationToken);
        var utcNow = _clock.UtcNow;

        var summaries = restaurants
            .OrderBy(restaurant => restaurant.Name, StringComparer.OrdinalIgnoreCase)
            .Select(restaurant =>
            {
                var localTime = _restaurantLocalTimeProvider.GetLocalTime(restaurant, utcNow);

                return new RestaurantSummary(
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.Description,
                    restaurant.ImageUrl,
                    restaurant.IsOpenAt(localTime));
            })
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<RestaurantSummary>>.Success(summaries);
    }
}
