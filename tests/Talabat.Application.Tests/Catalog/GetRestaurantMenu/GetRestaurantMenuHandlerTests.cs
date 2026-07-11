using Talabat.Application.Catalog.GetRestaurantMenu;
using Talabat.Application.Common.Results;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Catalog.GetRestaurantMenu;

public sealed class GetRestaurantMenuHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMenuWithUnavailableProductsFlagged()
    {
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(TestData.CreateRestaurant());
        var handler = new GetRestaurantMenuHandler(restaurants);

        var result = await handler.Handle(new GetRestaurantMenuQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Products.Count);
        Assert.Contains(result.Value.Products, product => product.IsAvailable);
        Assert.Contains(result.Value.Products, product => !product.IsAvailable);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenRestaurantDoesNotExist()
    {
        var handler = new GetRestaurantMenuHandler(new FakeRestaurantRepository());

        var result = await handler.Handle(new GetRestaurantMenuQuery(404));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.RestaurantNotFound, result.Error?.Code);
    }
}
