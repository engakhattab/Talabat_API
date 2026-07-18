using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.UserDomain.Users;

public class UserAddressInvariantTests
{
    private static User CreateCustomerWithProfile()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, null);
        return user;
    }

    private static UserAddress AddAddressWithId(User user, Address address, bool makeDefault = false, int id = 1)
    {
        user.AddAddress(address, makeDefault);
        var added = user.Addresses.First(a => a.Details == address);
        TestIds.SetId(added, id);
        return added;
    }

    [Fact]
    public void AddAddress_SecondDifferent_ShouldAddBoth()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street1", "City1", "1"));
        user.AddAddress(new Address("Street2", "City2", "2"));

        Assert.Equal(2, user.Addresses.Count);
    }

    [Fact]
    public void AddAddress_DuplicateExact_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        var address = new Address("Street", "City", "123");

        user.AddAddress(address);

        var act = () => user.AddAddress(new Address("Street", "City", "123"));

        Assert.Throws<DuplicateAddressException>(act);
    }

    [Fact]
    public void AddAddress_DuplicateDifferentCase_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.AddAddress(new Address("STREET", "CITY", "123"));

        Assert.Throws<DuplicateAddressException>(act);
    }

    [Fact]
    public void AddAddress_DuplicateWithFloor_ShouldThrowWhenFloorEqual()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street", "City", "123", "1"));

        var act = () => user.AddAddress(new Address("Street", "City", "123", "1"));

        Assert.Throws<DuplicateAddressException>(act);
    }

    [Fact]
    public void AddAddress_DifferentFloor_ShouldNotThrow()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street", "City", "123", "1"));
        user.AddAddress(new Address("Street", "City", "123", "2"));

        Assert.Equal(2, user.Addresses.Count);
    }

    [Fact]
    public void AddAddress_NoDefault_ShouldHaveNoDefault()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street", "City", "123"));

        Assert.False(user.Addresses.First().IsDefault);
    }

    [Fact]
    public void AddAddress_MakeDefault_ShouldMarkAsDefault()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street", "City", "123"), makeDefault: true);

        Assert.True(user.Addresses.First().IsDefault);
    }

    [Fact]
    public void AddAddress_TwoDefaults_ShouldClearFirst()
    {
        var user = CreateCustomerWithProfile();

        user.AddAddress(new Address("Street1", "City1", "1"), makeDefault: true);
        user.AddAddress(new Address("Street2", "City2", "2"), makeDefault: true);

        Assert.False(user.Addresses.ElementAt(0).IsDefault);
        Assert.True(user.Addresses.ElementAt(1).IsDefault);
    }

    [Fact]
    public void AddAddress_DuplicateDefaultRequest_ShouldPreserveExistingDefault()
    {
        var user = CreateCustomerWithProfile();
        var defaultAddress = new Address("Street1", "City1", "1");
        var duplicateAddress = new Address("Street2", "City2", "2");
        user.AddAddress(defaultAddress, makeDefault: true);
        user.AddAddress(duplicateAddress);

        var act = () => user.AddAddress(
            new Address("STREET2", "CITY2", "2"),
            makeDefault: true);

        Assert.Throws<DuplicateAddressException>(act);
        Assert.True(user.Addresses.Single(address => address.Details == defaultAddress).IsDefault);
        Assert.False(user.Addresses.Single(address => address.Details == duplicateAddress).IsDefault);
        Assert.Equal(2, user.Addresses.Count);
    }

    [Fact]
    public void SetDefaultAddress_ShouldSetDefault()
    {
        var user = CreateCustomerWithProfile();
        var addr1 = AddAddressWithId(user, new Address("Street1", "City1", "1"), id: 1);
        var addr2 = AddAddressWithId(user, new Address("Street2", "City2", "2"), id: 2);

        user.SetDefaultAddress(addr2.Id);

        Assert.False(addr1.IsDefault);
        Assert.True(addr2.IsDefault);
    }

    [Fact]
    public void SetDefaultAddress_NonPositiveId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.SetDefaultAddress(0);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void SetDefaultAddress_UnknownId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.SetDefaultAddress(999);

        Assert.Throws<AddressNotFoundException>(act);
    }

    [Fact]
    public void SetDefaultAddress_UnknownId_ShouldPreserveExistingDefault()
    {
        var user = CreateCustomerWithProfile();
        var selected = AddAddressWithId(
            user,
            new Address("Street", "City", "123"),
            makeDefault: true,
            id: 1);

        var act = () => user.SetDefaultAddress(999);

        Assert.Throws<AddressNotFoundException>(act);
        Assert.True(selected.IsDefault);
    }

    [Fact]
    public void RemoveAddress_ShouldRemove()
    {
        var user = CreateCustomerWithProfile();
        var addr = AddAddressWithId(user, new Address("Street", "City", "123"), id: 1);

        user.RemoveAddress(addr.Id);

        Assert.Empty(user.Addresses);
    }

    [Fact]
    public void RemoveAddress_NonPositiveId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.RemoveAddress(0);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void RemoveAddress_UnknownId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.RemoveAddress(999);

        Assert.Throws<AddressNotFoundException>(act);
    }

    [Fact]
    public void RemoveDefaultAddress_ShouldLeaveNoDefault()
    {
        var user = CreateCustomerWithProfile();
        var addr = AddAddressWithId(user, new Address("Street", "City", "123"), makeDefault: true, id: 1);

        user.RemoveAddress(addr.Id);

        Assert.Empty(user.Addresses);
    }

    [Fact]
    public void CreateDeliveryAddressSnapshot_ShouldReturnSnapshot()
    {
        var user = CreateCustomerWithProfile();
        var addr = AddAddressWithId(user, new Address("123 Main St", "Cairo", "42", "3"), id: 1);

        var snapshot = user.CreateDeliveryAddressSnapshot(addr.Id);

        Assert.Equal("123 Main St", snapshot.Street);
        Assert.Equal("Cairo", snapshot.City);
        Assert.Equal("42", snapshot.BuildingNumber);
        Assert.Equal("3", snapshot.Floor);
    }

    [Fact]
    public void CreateDeliveryAddressSnapshot_UnknownId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.CreateDeliveryAddressSnapshot(999);

        Assert.Throws<AddressNotFoundException>(act);
    }

    [Fact]
    public void CreateDeliveryAddressSnapshot_NonPositiveId_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();
        user.AddAddress(new Address("Street", "City", "123"));

        var act = () => user.CreateDeliveryAddressSnapshot(0);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void AddAddress_NullAddress_ShouldThrow()
    {
        var user = CreateCustomerWithProfile();

        var act = () => user.AddAddress(null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void RemoveAddress_ShouldNotAffectOtherAddresses()
    {
        var user = CreateCustomerWithProfile();
        var addr1 = AddAddressWithId(user, new Address("Street1", "City1", "1"), id: 1);
        var addr2 = AddAddressWithId(user, new Address("Street2", "City2", "2"), id: 2);

        user.RemoveAddress(addr2.Id);

        Assert.Single(user.Addresses);
        Assert.Equal(addr1.Id, user.Addresses.First().Id);
    }

    [Fact]
    public void CustomerAddressOperations_WithoutCustomerCapability_ShouldThrowCustomerGuard()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        Assert.Throws<CustomerProfileNotInitializedException>(() => user.RemoveAddress(1));
        Assert.Throws<CustomerProfileNotInitializedException>(() => user.SetDefaultAddress(1));
        Assert.Throws<CustomerProfileNotInitializedException>(
            () => user.CreateDeliveryAddressSnapshot(1));
        Assert.Empty(user.Addresses);
    }
}
