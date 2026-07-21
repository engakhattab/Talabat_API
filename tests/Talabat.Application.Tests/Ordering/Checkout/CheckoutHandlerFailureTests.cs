using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.DomainServices.Checkout;

namespace Talabat.Application.Tests.Ordering.Checkout;

public sealed class CheckoutHandlerFailureTests
{
    [Fact]
    public async Task Handle_ReturnsNotFoundWhenCustomerMissing()
    {
        var fixture = CreateFixture();
        fixture.Users.Users.Clear();

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CustomerNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenAddressMissing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AddressNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCartForEmptyCart()
    {
        var fixture = CreateFixture();
        fixture.Cart.RemoveItem(11, TestData.UtcNow);

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.EmptyCart, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsExpiredForExpiredCart()
    {
        var fixture = CreateFixture(clock: new FakeClock { UtcNow = TestData.UtcNow.AddHours(2) });

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartExpired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCartNotActiveForDuplicateCheckoutSubmission()
    {
        var fixture = CreateFixture();
        fixture.Cart.MarkCheckedOut(TestData.UtcNow);

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CartNotActive, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsUnavailableWhenRestaurantInactive()
    {
        var fixture = CreateFixture();
        fixture.Restaurant.Deactivate();

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.RestaurantInactive, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsUnavailableWhenRestaurantClosed()
    {
        var fixture = CreateFixture(localTime: new TimeOnly(23, 59));

        var result = await fixture.Handler.Handle(new CheckoutCommand(1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.RestaurantClosed, result.Error?.Code);
    }

    private static CheckoutFixture CreateFixture(
        FakeClock? clock = null,
        TimeOnly? localTime = null)
    {
        var restaurant = TestData.CreateRestaurant();
        var cart = TestData.CreateCart(restaurant: restaurant);
        var carts = new FakeCartRepository { CartToReturn = cart };
        carts.Carts.Add(cart);
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var restaurants = new FakeRestaurantRepository();
        restaurants.Restaurants.Add(restaurant);
        var handler = new CheckoutHandler(
            carts,
            users,
            restaurants,
            new FakeOrderRepository(),
            new FakeRestaurantLocalTimeProvider { LocalTime = localTime ?? new TimeOnly(12, 0) },
            clock ?? new FakeClock(),
            new FakeUnitOfWork(),
            new CheckoutDomainService());

        return new CheckoutFixture(handler, restaurant, cart, users);
    }

    private sealed record CheckoutFixture(
        CheckoutHandler Handler,
        Talabat.Domain.Aggregates.Catalog.Restaurant Restaurant,
        Talabat.Domain.Aggregates.Basket.Cart Cart,
        FakeUserRepository Users);
}
