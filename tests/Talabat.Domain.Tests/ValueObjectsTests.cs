using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Tests;

public sealed class ValueObjectsTests
{
    [Fact]
    public void Money_Zero_ShouldHaveZeroAmount()
    {
        var money = Money.Zero;

        Assert.Equal(0m, money.Amount);
    }

    [Fact]
    public void Money_Add_ShouldSumAmounts()
    {
        var m1 = new Money(10m);
        var m2 = new Money(20m);

        var result = m1.Add(m2);

        Assert.Equal(30m, result.Amount);
    }

    [Fact]
    public void Money_Multiply_ShouldMultiplyAmount()
    {
        var money = new Money(10m);

        var result = money.Multiply(3);

        Assert.Equal(30m, result.Amount);
    }

    [Fact]
    public void Money_Negative_ShouldThrow()
    {
        Assert.ThrowsAny<Exception>(() => new Money(-1m));
    }

    [Fact]
    public void Money_EqualAmounts_ShouldBeEqual()
    {
        var m1 = new Money(10m);
        var m2 = new Money(10m);

        Assert.Equal(m1, m2);
    }

    [Fact]
    public void Money_DifferentAmounts_ShouldNotBeEqual()
    {
        var m1 = new Money(10m);
        var m2 = new Money(20m);

        Assert.NotEqual(m1, m2);
    }

    [Fact]
    public void Money_Comparison_ShouldWork()
    {
        var m1 = new Money(10m);
        var m2 = new Money(20m);

        Assert.True(m1.CompareTo(m2) < 0);
        Assert.True(m2.CompareTo(m1) > 0);
    }

    [Fact]
    public void GeoLocation_ValidCoordinates_ShouldBeCreated()
    {
        var geo = new GeoLocation(40.7128m, -74.0060m);

        Assert.Equal(40.7128m, geo.Latitude);
        Assert.Equal(-74.0060m, geo.Longitude);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    public void GeoLocation_InvalidLatitude_ShouldThrow(double lat, double lng)
    {
        Assert.ThrowsAny<Exception>(() => new GeoLocation((decimal)lat, (decimal)lng));
    }

    [Theory]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void GeoLocation_InvalidLongitude_ShouldThrow(double lat, double lng)
    {
        Assert.ThrowsAny<Exception>(() => new GeoLocation((decimal)lat, (decimal)lng));
    }

    [Fact]
    public void GeoLocation_EqualCoordinates_ShouldBeEqual()
    {
        var g1 = new GeoLocation(40.7128m, -74.0060m);
        var g2 = new GeoLocation(40.7128m, -74.0060m);

        Assert.Equal(g1, g2);
    }

    [Fact]
    public void TimeRange_Contains_ShouldWork()
    {
        var range = new TimeRange(new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.True(range.Contains(new TimeOnly(12, 0)));
        Assert.False(range.Contains(new TimeOnly(8, 0)));
        Assert.False(range.Contains(new TimeOnly(17, 0)));
    }

    [Fact]
    public void TimeRange_Overnight_ShouldWrap()
    {
        var range = new TimeRange(new TimeOnly(22, 0), new TimeOnly(6, 0));

        Assert.True(range.Contains(new TimeOnly(23, 0)));
        Assert.True(range.Contains(new TimeOnly(1, 0)));
        Assert.False(range.Contains(new TimeOnly(12, 0)));
    }

    [Fact]
    public void TimeRange_SameStartEnd_ShouldThrow()
    {
        Assert.ThrowsAny<Exception>(() => new TimeRange(new TimeOnly(12, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void DeliveryAddressSnapshot_ShouldBeImmutableRecord()
    {
        var snapshot = new DeliveryAddressSnapshot("Main St", "City", "123", "2");

        Assert.Equal("Main St", snapshot.Street);
        Assert.Equal("City", snapshot.City);
        Assert.Equal("123", snapshot.BuildingNumber);
        Assert.Equal("2", snapshot.Floor);
    }

    [Fact]
    public void CheckoutItemSnapshot_ShouldStoreValues()
    {
        var item = new CheckoutItemSnapshot(1, "Product", new Money(25m), 2);

        Assert.Equal(1, item.ProductId);
        Assert.Equal("Product", item.ProductName);
        Assert.Equal(25m, item.UnitPrice.Amount);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void CatalogProductSnapshot_ShouldStoreValues()
    {
        var snapshot = new CatalogProductSnapshot(1, 10, "Product", true);

        Assert.Equal(1, snapshot.ProductId);
        Assert.Equal(10, snapshot.RestaurantId);
        Assert.Equal("Product", snapshot.ProductName);
        Assert.True(snapshot.IsAvailable);
    }
}
