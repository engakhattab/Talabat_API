using Talabat.Application.Basket.GetCart;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Basket.GetCart;

public sealed class GetCartHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyCartWhenNoActiveCartExists()
    {
        var handler = new GetCartHandler(
            new FakeCartRepository(),
            new FakeRestaurantRepository(),
            new FakeClock());

        var result = await handler.Handle(new GetCartQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Id);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0m, result.Value.CalculatedCurrentTotal.Amount);
    }

    [Fact]
    public async Task Handle_CalculatesTotalFromCurrentCatalogPrices()
    {
        var restaurant = TestData.CreateRestaurant();
        restaurant.UpdateProductPrice(11, new(75m));
        var cart = TestData.CreateCart(restaurant: restaurant, quantity: 2);

        var carts = new FakeCartRepository();
        carts.Carts.Add(cart);
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);

        var handler = new GetCartHandler(carts, restaurants, new FakeClock());

        var result = await handler.Handle(new GetCartQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value.CalculatedCurrentTotal.Amount);
        Assert.Equal(75m, Assert.Single(result.Value.Items).CurrentUnitPrice.Amount);
    }
}
