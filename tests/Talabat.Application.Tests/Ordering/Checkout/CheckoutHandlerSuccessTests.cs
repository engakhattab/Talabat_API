using Talabat.Application.Fulfillment;
using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.DomainServices.Checkout;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Ordering.Checkout;

public sealed class CheckoutHandlerSuccessTests
{
    private readonly FakeDeliveryRepository _deliveries = new();
    private readonly FakeOrderFulfillmentRepository _fulfillments = new();

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
        var unitOfWork = new FakeUnitOfWork(orders, _deliveries, _fulfillments);
        var handler = CreateHandler(carts, users, restaurants, orders, unitOfWork);

        var result = await handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsSuccess);
        var outcome = Assert.IsType<CheckoutSucceededOutcome>(result.Value);
        Assert.Equal(160m, outcome.TotalAmount.Amount);
        Assert.Equal(1, orders.AddCount);
        Assert.Equal(1, carts.UpdateCount);
        // One transaction, two saves: the order (to obtain its id), then its order fulfillment.
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
        var createdOrder = Assert.Single(orders.Orders);
        Assert.Equal(createdOrder.Id, outcome.OrderId);
        Assert.True(outcome.OrderId > 0);
        Assert.Equal(160m, createdOrder.TotalAmount.Amount);
    }

    // Design §4 test 15 (D3; revises BR-DEL-001 and BR-DEL-016): checkout places the order with the
    // restaurant and no longer creates a delivery.
    [Fact]
    public async Task Checkout_creates_order_and_fulfillment_but_no_delivery()
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant);
        var carts = new FakeCartRepository { CartToReturn = cart };
        carts.Carts.Add(cart);
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork(orders, _deliveries, _fulfillments);
        var clock = new FakeClock();
        var handler = CreateHandler(carts, users, restaurants, orders, unitOfWork, clock);

        var result = await handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsSuccess);
        var order = Assert.Single(orders.Orders);
        var fulfillment = Assert.Single(_fulfillments.Fulfillments);
        Assert.True(fulfillment.Id > 0);
        Assert.Equal(order.Id, fulfillment.OrderId);
        Assert.Equal(restaurant.Id, fulfillment.RestaurantId);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, fulfillment.Status);
        Assert.Equal(clock.UtcNow, fulfillment.PlacedAt);
        Assert.Equal(clock.UtcNow.AddMinutes(10), fulfillment.AcceptanceDeadline);
        Assert.Empty(_deliveries.Deliveries);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    private CheckoutHandler CreateHandler(
        FakeCartRepository carts,
        FakeUserRepository users,
        FakeRestaurantRepository restaurants,
        FakeOrderRepository orders,
        FakeUnitOfWork unitOfWork,
        FakeClock? clock = null)
    {
        return new CheckoutHandler(
            carts,
            users,
            restaurants,
            orders,
            _fulfillments,
            new OrderFulfillmentOptions(),
            new FakeRestaurantLocalTimeProvider { LocalTime = new TimeOnly(12, 0) },
            clock ?? new FakeClock(),
            unitOfWork,
            new CheckoutDomainService());
    }
}
