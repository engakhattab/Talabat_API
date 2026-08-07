using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Tests.Basket;

public sealed class CartTests
{
    private static readonly DateTime Now =
        new(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsExpired_ReturnsTrueWhenCreatedAtIsOlderThanOneHour()
    {
        var cart = CreateCart(createdAt: Now.AddHours(-1));

        Assert.True(cart.IsExpired(Now));
    }

    [Fact]
    public void IsExpired_ReturnsFalseForFreshCart()
    {
        var cart = CreateCart(createdAt: Now);

        Assert.False(cart.IsExpired(Now));
    }

    [Fact]
    public void MarkExpired_TransitionsExpiredActiveCartToExpired()
    {
        var cart = CreateCart(createdAt: Now.AddHours(-2));

        cart.MarkExpired(Now);

        Assert.Equal(CartStatus.Expired, cart.Status);
    }

    [Fact]
    public void MarkExpired_ThrowsForActiveCartThatHasNotExpired()
    {
        var cart = CreateCart(createdAt: Now);

        Assert.Throws<CartNotActiveException>(() => cart.MarkExpired(Now));
        Assert.Equal(CartStatus.Active, cart.Status);
    }

    [Fact]
    public void MarkExpired_ThrowsForCheckedOutCart()
    {
        var cart = CreateCart(createdAt: Now.AddMinutes(-30));
        cart.MarkCheckedOut(Now.AddMinutes(-20));

        Assert.Throws<CartNotActiveException>(() => cart.MarkExpired(Now));
        Assert.Equal(CartStatus.CheckedOut, cart.Status);
    }

    private static Cart CreateCart(DateTime createdAt)
    {
        return Cart.Create(
            customerId: 1,
            new CatalogProductSnapshot(11, 1, "Koshary", isAvailable: true),
            quantity: 1,
            createdAt);
    }
}
