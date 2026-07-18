using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Catalog.BrowseRestaurants;
using Talabat.Application.Catalog.GetRestaurantMenu;
using Talabat.Application.Common.Results;
using Talabat.Customer.API.Contracts.Catalog;
using Talabat.Customer.API.Extensions;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly BrowseRestaurantsHandler _browseRestaurantsHandler;
    private readonly GetRestaurantMenuHandler _getRestaurantMenuHandler;

    public CatalogController(
        BrowseRestaurantsHandler browseRestaurantsHandler,
        GetRestaurantMenuHandler getRestaurantMenuHandler)
    {
        _browseRestaurantsHandler = browseRestaurantsHandler;
        _getRestaurantMenuHandler = getRestaurantMenuHandler;
    }

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _browseRestaurantsHandler.Handle(
            new BrowseRestaurantsQuery(),
            cancellationToken);

        return result.ToActionResult(restaurants =>
        {
            var items = restaurants.Select(r => new RestaurantSummaryDto(
                r.Id, r.Name, r.Description, r.ImageUrl, r.IsOpen)).ToList();

            return Ok(new RestaurantListResponse(items, page, pageSize, items.Count));
        });
    }

    [HttpGet("restaurants/{restaurantId:int}/menu")]
    public async Task<IActionResult> GetMenu(
        int restaurantId,
        CancellationToken cancellationToken)
    {
        var result = await _getRestaurantMenuHandler.Handle(
            new GetRestaurantMenuQuery(restaurantId),
            cancellationToken);

        return result.ToActionResult(menu =>
        {
            var products = menu.Products.Select(p => new MenuProductDto(
                p.Id, p.Name, p.Description,
                new Contracts.Common.MoneyDto(p.CurrentPrice.Amount),
                p.IsAvailable)).ToList();

            return Ok(new MenuResponse(menu.RestaurantId, menu.RestaurantName, products));
        });
    }
}
