using Talabat.Domain.Common;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Tests;

public sealed class AddressTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var address = new Address("Main St", "City", "123", "2");

        Assert.Equal("Main St", address.Street);
        Assert.Equal("City", address.City);
        Assert.Equal("123", address.BuildingNumber);
        Assert.Equal("2", address.Floor);
    }

    [Fact]
    public void Constructor_NullFloor_ShouldBeOptional()
    {
        var address = new Address("Main St", "City", "123", null);

        Assert.Null(address.Floor);
    }

    [Fact]
    public void Equals_SameValues_ShouldBeEqual()
    {
        var addr1 = new Address("main st", "city", "123", "2");
        var addr2 = new Address("Main St", "City", "123", "2");

        Assert.Equal(addr1, addr2);
    }

    [Fact]
    public void Equals_DifferentValues_ShouldNotBeEqual()
    {
        var addr1 = new Address("Main St", "City", "123", "2");
        var addr2 = new Address("Other St", "City", "123", "2");

        Assert.NotEqual(addr1, addr2);
    }

    [Fact]
    public void Equals_NullFloorVsNonNullFloor_ShouldNotBeEqual()
    {
        var addr1 = new Address("Main St", "City", "123", null);
        var addr2 = new Address("Main St", "City", "123", "2");

        Assert.NotEqual(addr1, addr2);
    }

    [Fact]
    public void GetHashCode_EqualValues_ShouldMatch()
    {
        var addr1 = new Address("main st", "city", "123", "2");
        var addr2 = new Address("Main St", "City", "123", "2");

        Assert.Equal(addr1.GetHashCode(), addr2.GetHashCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_InvalidStreet_ShouldThrow(string? street)
    {
        Assert.ThrowsAny<Exception>(() => new Address(street!, "City", "123", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_InvalidCity_ShouldThrow(string? city)
    {
        Assert.ThrowsAny<Exception>(() => new Address("Main St", city!, "123", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_InvalidBuildingNumber_ShouldThrow(string? building)
    {
        Assert.ThrowsAny<Exception>(() => new Address("Main St", "City", building!, null));
    }

    [Fact]
    public void OperatorEquals_SameValues_ShouldBeTrue()
    {
        var addr1 = new Address("Main St", "City", "123");
        var addr2 = new Address("Main St", "City", "123");

        Assert.True(addr1 == addr2);
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ShouldBeTrue()
    {
        var addr1 = new Address("Main St", "City", "123");
        var addr2 = new Address("Other St", "City", "123");

        Assert.True(addr1 != addr2);
    }
}
