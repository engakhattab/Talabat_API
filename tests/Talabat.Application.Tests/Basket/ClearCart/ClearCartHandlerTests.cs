using Talabat.Application.Basket.AddItem;
using Talabat.Application.Basket.ClearCart;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Basket.ClearCart;

public sealed class ClearCartHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDeterministicEmptyCartAndCommits()
    {
        var cart = TestData.CreateCart();
        var carts = new FakeCartRepository();
        carts.Carts.Add(cart);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ClearCartHandler(carts, new FakeClock(), unitOfWork);

        var result = await handler.Handle(new ClearCartCommand(1));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Id);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0m, result.Value.CalculatedCurrentTotal.Amount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task AddItemAfterClearCreatesNewCart()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant);
        var carts = new FakeCartRepository();
        carts.Carts.Add(cart);
        var clearHandler = new ClearCartHandler(carts, new FakeClock(), new FakeUnitOfWork());
        await clearHandler.Handle(new ClearCartCommand(1));
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var addHandler = new AddCartItemHandler(
            carts,
            restaurants,
            new FakeClock(),
            new FakeUnitOfWork(carts));

        var result = await addHandler.Handle(new AddCartItemCommand(1, 1, 11, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, carts.Carts.Count);
        Assert.Equal(carts.Carts.Last().Id, result.Value.Id);
        Assert.NotEqual(cart.Id, result.Value.Id);
        Assert.True(result.Value.Id > 0);
    }
}
