using Talabat.Application.Basket.AddItem;
using Talabat.Application.Common.Results;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Basket.AddItem;

public sealed class AddCartItemHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCartForFirstValidItem()
    {
        var restaurant = TestData.CreateRestaurant();
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var carts = new FakeCartRepository();
        var unitOfWork = new FakeUnitOfWork(carts);
        var handler = CreateHandler(carts, restaurants, unitOfWork);

        var result = await handler.Handle(new AddCartItemCommand(1, 1, 11, 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, carts.AddCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Equal(100m, result.Value.CalculatedCurrentTotal.Amount);
    }

    [Fact]
    public async Task Handle_ReturnsUnavailableForUnavailableProduct()
    {
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(TestData.CreateRestaurant());
        var carts = new FakeCartRepository();
        var handler = CreateHandler(carts, restaurants, new FakeUnitOfWork());

        var result = await handler.Handle(new AddCartItemCommand(1, 1, 21, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.ProductUnavailable, result.Error?.Code);
        Assert.Equal(0, carts.AddCount);
    }

    [Fact]
    public async Task Handle_ReturnsValidationForInvalidQuantity()
    {
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(TestData.CreateRestaurant());
        var handler = CreateHandler(new FakeCartRepository(), restaurants, new FakeUnitOfWork());

        var result = await handler.Handle(new AddCartItemCommand(1, 1, 11, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.InvalidQuantity, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsConflictAndPreservesCartForCrossRestaurantAdd()
    {
        var firstRestaurant = TestData.CreateRestaurant(id: 1);
        var secondRestaurant = TestData.CreateRestaurant(id: 2);
        var existingCart = TestData.CreateCart(restaurant: firstRestaurant);
        var carts = new FakeCartRepository { CartToReturn = existingCart };
        carts.Carts.Add(existingCart);
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(firstRestaurant);
        restaurants.Restaurants.Add(secondRestaurant);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(carts, restaurants, unitOfWork);

        var result = await handler.Handle(new AddCartItemCommand(1, 2, 12, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CrossRestaurantCart, result.Error?.Code);
        Assert.Single(existingCart.Items);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    private static AddCartItemHandler CreateHandler(
        FakeCartRepository carts,
        FakeRestaurantRepository restaurants,
        FakeUnitOfWork unitOfWork)
    {
        return new AddCartItemHandler(
            carts,
            restaurants,
            new FakeClock(),
            unitOfWork);
    }
}
