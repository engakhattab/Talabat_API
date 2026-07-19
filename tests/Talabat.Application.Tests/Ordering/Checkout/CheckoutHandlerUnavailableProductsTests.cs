using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.DomainServices.Checkout;

namespace Talabat.Application.Tests.Ordering.Checkout;

public sealed class CheckoutHandlerUnavailableProductsTests
{
    [Fact]
    public async Task Handle_ReturnsUnavailableProductsWithoutCreatingOrderOrClosingCart()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant);
        restaurant.MarkProductUnavailable(11);
        var carts = new FakeCartRepository { CartToReturn = cart };
        carts.Carts.Add(cart);
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CheckoutHandler(
            carts,
            users,
            restaurants,
            orders,
            new FakeRestaurantLocalTimeProvider { LocalTime = new TimeOnly(12, 0) },
            new FakeClock(),
            unitOfWork,
            new CheckoutDomainService());

        var result = await handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsSuccess);
        var outcome = Assert.IsType<CheckoutProductsUnavailableOutcome>(result.Value);
        var unavailableItem = Assert.Single(outcome.UnavailableItems);
        Assert.Equal(11, unavailableItem.ProductId);
        Assert.Equal(0, orders.AddCount);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal("Active", cart.Status.ToString());
    }
}
