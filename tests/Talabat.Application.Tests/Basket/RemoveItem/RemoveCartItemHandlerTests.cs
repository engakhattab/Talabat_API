using Talabat.Application.Basket.RemoveItem;
using Talabat.Application.Common.Results;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Basket.RemoveItem;

public sealed class RemoveCartItemHandlerTests
{
    [Fact]
    public async Task Handle_RemovesItemAndCommits()
    {
        var restaurant = TestData.CreateRestaurant();
        var carts = new FakeCartRepository();
        carts.Carts.Add(TestData.CreateCart(restaurant: restaurant));
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RemoveCartItemHandler(carts, restaurants, new FakeClock(), unitOfWork);

        var result = await handler.Handle(new RemoveCartItemCommand(1, 11));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0m, result.Value.CalculatedCurrentTotal.Amount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenItemMissing()
    {
        var restaurant = TestData.CreateRestaurant();
        var carts = new FakeCartRepository();
        carts.Carts.Add(TestData.CreateCart(restaurant: restaurant));
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var handler = new RemoveCartItemHandler(carts, restaurants, new FakeClock(), new FakeUnitOfWork());

        var result = await handler.Handle(new RemoveCartItemCommand(1, 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartItemNotFound, result.Error?.Code);
    }
}
