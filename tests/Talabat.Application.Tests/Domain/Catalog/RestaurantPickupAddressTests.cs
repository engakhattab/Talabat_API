using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.Catalog;

public sealed class RestaurantPickupAddressTests
{
    private static readonly TimeRange Hours =
        new(new TimeOnly(8, 0), new TimeOnly(22, 0));

    [Fact]
    public void Constructor_RequiresPickupAddress()
    {
        Assert.Throws<ArgumentNullException>(() => new Restaurant(
            "Test Restaurant",
            "Test description",
            null,
            Hours,
            null!));
    }

    [Fact]
    public void Constructor_RetainsValidatedPickupAddress()
    {
        var pickupAddress = new Address("10 Pickup Street", "Cairo", "10", "2");

        var restaurant = new Restaurant(
            "Test Restaurant",
            "Test description",
            null,
            Hours,
            pickupAddress);

        Assert.Equal(pickupAddress, restaurant.PickupAddress);
    }
}
