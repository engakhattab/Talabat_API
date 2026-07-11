using Talabat.Application.Basket.UpdateQuantity;
using Talabat.Application.Common.Results;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Basket.UpdateQuantity;

public sealed class UpdateCartItemQuantityHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesQuantityAndCurrentTotal()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant, quantity: 1);
        var carts = new FakeCartRepository();
        carts.Carts.Add(cart);
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCartItemQuantityHandler(carts, restaurants, new FakeClock(), unitOfWork);

        var result = await handler.Handle(new UpdateCartItemQuantityCommand(1, 11, 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value.CalculatedCurrentTotal.Amount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, ApplicationErrorCodes.InvalidQuantity)]
    [InlineData(-1, ApplicationErrorCodes.InvalidQuantity)]
    public async Task Handle_ReturnsValidationForInvalidQuantity(int quantity, string expectedCode)
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new UpdateCartItemQuantityCommand(1, 11, quantity));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsExpiredWhenCartExpired()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant, createdAt: TestData.UtcNow);
        var carts = new FakeCartRepository { CartToReturn = cart };
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var clock = new FakeClock { UtcNow = TestData.UtcNow.AddHours(2) };
        var handler = new UpdateCartItemQuantityHandler(carts, restaurants, clock, new FakeUnitOfWork());

        var result = await handler.Handle(new UpdateCartItemQuantityCommand(1, 11, 2));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartExpired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCartNotActiveForNonActiveCart()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant);
        cart.MarkCheckedOut(TestData.UtcNow);
        var carts = new FakeCartRepository { CartToReturn = cart };
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var handler = new UpdateCartItemQuantityHandler(carts, restaurants, new FakeClock(), new FakeUnitOfWork());

        var result = await handler.Handle(new UpdateCartItemQuantityCommand(1, 11, 2));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartNotActive, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenItemMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(new UpdateCartItemQuantityCommand(1, 999, 2));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartItemNotFound, result.Error?.Code);
    }

    private static (UpdateCartItemQuantityHandler Handler, FakeUnitOfWork UnitOfWork) CreateFixture()
    {
        var restaurant = TestData.CreateRestaurant();
        var carts = new FakeCartRepository();
        carts.Carts.Add(TestData.CreateCart(restaurant: restaurant));
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        return (new UpdateCartItemQuantityHandler(carts, restaurants, new FakeClock(), unitOfWork), unitOfWork);
    }
}
