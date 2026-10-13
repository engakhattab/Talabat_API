using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.UserDomain.Users;

public class UserCustomerCapabilityTests
{
    [Fact]
    public void InitializeCustomerProfile_ShouldSetFieldsAndAddCustomerFlag()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        user.InitializeCustomerProfile("John Doe", 25, "123456");

        Assert.Equal("John Doe", user.FullName);
        Assert.Equal(25, user.Age);
        Assert.Equal("123456", user.PhoneNumber);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
    }

    [Fact]
    public void InitializeCustomerProfile_ShouldNormalizePhoneNumber()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        user.InitializeCustomerProfile("John Doe", 25, "  123456  ");

        Assert.Equal("123456", user.PhoneNumber);
    }

    [Fact]
    public void InitializeCustomerProfile_ShouldThrowOnNullFullName()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        var act = () => user.InitializeCustomerProfile(null!, 25, null);

        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("fullName", ex.ParamName);
    }

    [Fact]
    public void InitializeCustomerProfile_ShouldThrowOnNonPositiveAge()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        var act = () => user.InitializeCustomerProfile("John Doe", 0, null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("age", ex.ParamName);
    }

    [Fact]
    public void InitializeCustomerProfile_InvalidInput_ShouldLeaveExistingStateUnchanged()
    {
        var user = User.Register("john_doe", "john@example.com", "Original Name");
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.ApproveDeliveryAgentApplication();

        var act = () => user.InitializeCustomerProfile("Replacement Name", 0, "replacement");

        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("Original Name", user.FullName);
        Assert.Null(user.Age);
        Assert.Null(user.PhoneNumber);
        Assert.Equal(UserType.DeliveryAgent, user.UserType);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void InitializeCustomerProfile_ShouldThrowOnNegativeAge()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        var act = () => user.InitializeCustomerProfile("John Doe", -5, null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("age", ex.ParamName);
    }

    [Fact]
    public void UpdateCustomerProfile_ShouldUpdateFields()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, "123456");

        user.UpdateCustomerProfile("John Updated", 30, "789012");

        Assert.Equal("John Updated", user.FullName);
        Assert.Equal(30, user.Age);
        Assert.Equal("789012", user.PhoneNumber);
    }

    [Fact]
    public void UpdateCustomerProfile_ShouldPreserveCustomerFlag()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, "123456");

        user.UpdateCustomerProfile("John Updated", 30, "789012");

        Assert.True(user.UserType.HasFlag(UserType.Customer));
    }

    [Fact]
    public void UpdateCustomerProfile_InvalidInput_ShouldLeaveProfileUnchanged()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, "123456");

        var act = () => user.UpdateCustomerProfile("Replacement", -1, "replacement");

        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("John Doe", user.FullName);
        Assert.Equal(25, user.Age);
        Assert.Equal("123456", user.PhoneNumber);
        Assert.Equal(UserType.Customer, user.UserType);
    }

    [Fact]
    public void UpdateCustomerProfile_ShouldThrowWithoutCustomerFlag()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        var act = () => user.UpdateCustomerProfile("John Doe", 25, null);

        Assert.Throws<CustomerProfileNotInitializedException>(act);
    }

    [Fact]
    public void AddAddress_WithoutCustomerFlag_ShouldThrow()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        var address = new Address("Street", "City", "123");

        var act = () => user.AddAddress(address);

        Assert.Throws<CustomerProfileNotInitializedException>(act);
    }

    [Fact]
    public void AddAddress_WithCustomerFlag_ShouldAdd()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, null);
        var address = new Address("Street", "City", "123");

        user.AddAddress(address);

        Assert.Single(user.Addresses);
    }

    [Fact]
    public void AddAddress_Duplicate_ShouldThrow()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, null);
        var address = new Address("Street", "City", "123");

        user.AddAddress(address);

        var act = () => user.AddAddress(new Address("Street", "City", "123"));

        Assert.Throws<DuplicateAddressException>(act);
    }

    [Fact]
    public void AddAddress_Duplicate_DifferentCase_ShouldThrow()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, null);
        var address = new Address("Street", "City", "123");

        user.AddAddress(address);

        var act = () => user.AddAddress(new Address("street", "city", "123"));

        Assert.Throws<DuplicateAddressException>(act);
    }

    [Fact]
    public void AddAddress_MakeDefault_ShouldSetDefault()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, null);
        var address = new Address("Street", "City", "123");

        user.AddAddress(address, makeDefault: true);

        Assert.True(user.Addresses.First().IsDefault);
    }

    [Fact]
    public void InitializeCustomerProfile_CalledTwice_ShouldReplaceValues()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.InitializeCustomerProfile("John Doe", 25, "111");

        user.InitializeCustomerProfile("John Updated", 30, "222");

        Assert.Equal("John Updated", user.FullName);
        Assert.Equal(30, user.Age);
        Assert.Equal("222", user.PhoneNumber);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
    }

    [Fact]
    public void InitializeAndUpdateCustomerProfile_ShouldPreserveAgentCapability()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");
        user.SubmitDeliveryAgentApplication(VehicleType.Car);
        user.ApproveDeliveryAgentApplication();

        user.InitializeCustomerProfile("John Customer", 25, "111");
        user.UpdateCustomerProfile("John Updated", 30, "222");

        Assert.Equal(UserType.Customer | UserType.DeliveryAgent, user.UserType);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.Equal("John Updated", user.FullName);
        Assert.Equal(30, user.Age);
        Assert.Equal("222", user.PhoneNumber);
    }

    [Fact]
    public void InitializeCustomerProfile_ShouldSetPhoneNumber()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        user.InitializeCustomerProfile("John Doe", 25, "555-1234");

        Assert.Equal("555-1234", user.PhoneNumber);
    }

    [Fact]
    public void InitializeCustomerProfile_NullPhoneNumber_ShouldBeNull()
    {
        var user = User.Register("john_doe", "john@example.com", "John Doe");

        user.InitializeCustomerProfile("John Doe", 25, null);

        Assert.Null(user.PhoneNumber);
    }
}
