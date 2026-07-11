using Talabat.Application.Catalog.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Catalog.GetRestaurantMenu;

public sealed class GetRestaurantMenuHandler
{
    private readonly IRestaurantRepository _restaurantRepository;

    public GetRestaurantMenuHandler(IRestaurantRepository restaurantRepository)
    {
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
    }

    public async Task<UseCaseResult<RestaurantMenu>> Handle(
        GetRestaurantMenuQuery query,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await _restaurantRepository.GetByIdWithProductsAsync(
            query.RestaurantId,
            cancellationToken);

        if (restaurant is null)
        {
            return UseCaseResult<RestaurantMenu>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.RestaurantNotFound,
                    "Restaurant was not found."));
        }

        var products = restaurant.Products
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .Select(product => new MenuProduct(
                product.Id,
                product.Name,
                product.Description,
                product.CurrentPrice,
                product.ImageUrl,
                product.IsAvailable))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<RestaurantMenu>.Success(
            new RestaurantMenu(restaurant.Id, restaurant.Name, products));
    }
}
