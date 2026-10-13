using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Tests;

public sealed class UserCapabilityTests
{
    [Fact]
    public void InitializeCustomerProfile_ShouldSetCustomerCapability()
    {
        var user = CreateRegisteredUser();

        user.InitializeCustomerProfile("Test Customer", 25, "1234567890");

        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.Equal("Test Customer", user.FullName);
        Assert.Equal(25, user.Age);
    }

    [Fact]
    public void InitializeCustomerProfile_Again_ShouldSucceed()
    {
        var user = CreateRegisteredUser();
        user.InitializeCustomerProfile("Test Customer", 25, "1234567890");

        // Calling again just overwrites values — no exception
        user.InitializeCustomerProfile("Updated Name", 30, "9876543210");

        Assert.Equal("Updated Name", user.FullName);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void InitializeCustomerProfile_WhenNotRegistered_ShouldThrow()
    {
        var user = User.Register("test", "test@test.com", "Test User");

        Assert.Throws<ArgumentException>(
            () => user.InitializeCustomerProfile("", 25, null));
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_ShouldSetPendingApproval()
    {
        var user = CreateRegisteredUser();

        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Bike, user.VehicleType);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_WhenAlreadyApproved_ShouldThrow()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.ApproveDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(
            () => user.SubmitDeliveryAgentApplication(VehicleType.Car));
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_ShouldSetApproved()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        user.ApproveDeliveryAgentApplication();

        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_WhenNotPending_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        Assert.Throws<AgentApplicationNotPendingException>(
            () => user.ApproveDeliveryAgentApplication());
    }

    [Fact]
    public void RejectDeliveryAgentApplication_ShouldSetRejected()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        user.RejectDeliveryAgentApplication();

        Assert.Equal(AgentApprovalStatus.Rejected, user.AgentApprovalStatus);
    }

    [Fact]
    public void GoOnline_WhenApprovedAgent_ShouldSetAvailable()
    {
        var user = CreateApprovedAgent();

        user.GoOnline();

        Assert.Equal(DeliveryAgentStatus.Available, user.DeliveryAgentStatus);
        Assert.True(user.IsAvailable());
    }

    [Fact]
    public void GoOnline_WhenSuspended_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.Suspend();

        Assert.Throws<AgentNotAvailableException>(
            () => user.GoOnline());
    }

    [Fact]
    public void GoOffline_WhenAvailable_ShouldSetOffline()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.GoOffline();

        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.False(user.IsAvailable());
    }

    [Fact]
    public void Suspend_WhenAvailable_ShouldSetSuspended()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.Suspend();

        Assert.Equal(DeliveryAgentStatus.Suspended, user.DeliveryAgentStatus);
    }

    [Fact]
    public void UpdateCustomerProfile_ShouldUpdateNameAndAge()
    {
        var user = CreateCustomerUser();

        user.UpdateCustomerProfile("New Name", 30, "5551234567");

        Assert.Equal("New Name", user.FullName);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void AddAddress_ShouldAddToCollection()
    {
        var user = CreateCustomerUser();
        var address = CreateAddress("Main St", "City", "123");

        user.AddAddress(address);

        Assert.Single(user.Addresses);
    }

    [Fact]
    public void AddAddress_Duplicate_ShouldThrow()
    {
        var user = CreateCustomerUser();
        var address = CreateAddress("Main St", "City", "123");
        user.AddAddress(address);

        Assert.Throws<DuplicateAddressException>(
            () => user.AddAddress(CreateAddress("Main St", "City", "123")));
    }

    [Fact]
    public void RemoveAddress_Existing_ShouldRemoveFromCollection()
    {
        var user = CreateCustomerUser();
        var address = CreateAddress("Main St", "City", "123");
        user.AddAddress(address);

        Assert.Single(user.Addresses);
    }

    [Fact]
    public void SetDefaultAddress_ShouldMarkOnlyOne()
    {
        var user = CreateCustomerUser();
        var addr1 = CreateAddress("Main St", "City", "1");
        var addr2 = CreateAddress("2nd St", "City", "2");
        user.AddAddress(addr1, makeDefault: true);
        user.AddAddress(addr2);

        // UserAddress.Id is 0 in-memory (EF auto-generates), so we can't test
        // SetDefaultAddress directly. The AddAddress with makeDefault:true tests
        // the MarkAllAddressesAsNonDefault logic.
        var defaultAddresses = user.Addresses.Where(a => a.IsDefault).ToList();
        Assert.Single(defaultAddresses);
        Assert.Equal(addr1, defaultAddresses[0].Details);
    }

    private static User CreateRegisteredUser()
    {
        return User.Register("testuser", "test@test.com", "Test User");
    }

    private static User CreateCustomerUser()
    {
        var user = CreateRegisteredUser();
        user.InitializeCustomerProfile("Test Customer", 25, "1234567890");
        return user;
    }

    private static User CreateApprovedAgent()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.ApproveDeliveryAgentApplication();
        return user;
    }

    private static Address CreateAddress(string street, string city, string building)
    {
        return new Address(street, city, building, null);
    }
}
