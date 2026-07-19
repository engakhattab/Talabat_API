using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.DomainServices.Checkout;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Ordering.Checkout;

public sealed class CheckoutHandlerSuccessTests
{
    [Fact]
    public async Task Handle_CreatesOneOrderChecksOutOneCartUsesCurrentCatalogPriceAndCommitsOnce()
    {
        var restaurant = TestData.CreateRestaurant();
        restaurant.UpdateProductPrice(11, new Money(80m));
        var cart = TestData.CreateCart(restaurant: restaurant, quantity: 2);
        var carts = new FakeCartRepository { CartToReturn = cart };
        carts.Carts.Add(cart);
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork(orders);
        var handler = CreateHandler(carts, users, restaurants, orders, unitOfWork);

        var result = await handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsSuccess);
        var outcome = Assert.IsType<CheckoutSucceededOutcome>(result.Value);
        Assert.Equal(160m, outcome.TotalAmount.Amount);
        Assert.Equal(1, orders.AddCount);
        Assert.Equal(1, carts.UpdateCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        var createdOrder = Assert.Single(orders.Orders);
        Assert.Equal(createdOrder.Id, outcome.OrderId);
        Assert.True(outcome.OrderId > 0);
        Assert.Equal(160m, createdOrder.TotalAmount.Amount);
    }

    private static CheckoutHandler CreateHandler(
        FakeCartRepository carts,
        FakeUserRepository users,
        FakeRestaurantRepository restaurants,
        FakeOrderRepository orders,
        FakeUnitOfWork unitOfWork)
    {
        return new CheckoutHandler(
            carts,
            users,
            restaurants,
            orders,
            new FakeRestaurantLocalTimeProvider { LocalTime = new TimeOnly(12, 0) },
            new FakeClock(),
            unitOfWork,
            new CheckoutDomainService());
    }
}
