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
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(carts, customers, restaurants, orders, unitOfWork);

        var result = await handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsSuccess);
        var outcome = Assert.IsType<CheckoutSucceededOutcome>(result.Value);
        Assert.Equal(300, outcome.OrderId);
        Assert.Equal(160m, outcome.TotalAmount.Amount);
        Assert.Equal(1, orders.AddCount);
        Assert.Equal(1, carts.UpdateCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Single(orders.Orders);
        Assert.Equal(160m, orders.Orders[0].TotalAmount.Amount);
    }

    private static CheckoutHandler CreateHandler(
        FakeCartRepository carts,
        FakeCustomerRepository customers,
        FakeRestaurantRepository restaurants,
        FakeOrderRepository orders,
        FakeUnitOfWork unitOfWork)
    {
        return new CheckoutHandler(
            carts,
            customers,
            restaurants,
            orders,
            new FakeApplicationIdGenerator(),
            new FakeRestaurantLocalTimeProvider { LocalTime = new TimeOnly(12, 0) },
            new FakeClock(),
            unitOfWork,
            new CheckoutDomainService());
    }
}
