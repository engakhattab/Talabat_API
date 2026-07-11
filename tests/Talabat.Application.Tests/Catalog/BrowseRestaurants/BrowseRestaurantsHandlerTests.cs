using Talabat.Application.Catalog.BrowseRestaurants;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Catalog.BrowseRestaurants;

public sealed class BrowseRestaurantsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyActiveRestaurantsWithOpenStatus()
    {
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(TestData.CreateRestaurant(id: 1, active: true));
        restaurants.Restaurants.Add(TestData.CreateRestaurant(id: 2, active: false));

        var handler = new BrowseRestaurantsHandler(
            restaurants,
            new FakeRestaurantLocalTimeProvider { LocalTime = new TimeOnly(12, 0) },
            new FakeClock());

        var result = await handler.Handle(new BrowseRestaurantsQuery());

        Assert.True(result.IsSuccess);
        var restaurant = Assert.Single(result.Value);
        Assert.Equal(1, restaurant.Id);
        Assert.True(restaurant.IsOpen);
    }
}
