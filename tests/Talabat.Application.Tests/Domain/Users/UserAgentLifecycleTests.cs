using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.UserDomain.Users;

public class UserAgentLifecycleTests
{
    [Fact]
    public void SubmitDeliveryAgentApplication_ShouldSetPending()
    {
        var user = CreateRegisteredUser();

        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Bike, user.VehicleType);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_InvalidVehicle_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.SubmitDeliveryAgentApplication((VehicleType)99);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("vehicleType", ex.ParamName);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_InvalidVehicle_ShouldPreservePendingApplication()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        var act = () => user.SubmitDeliveryAgentApplication((VehicleType)99);

        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Bike, user.VehicleType);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_WhenAlreadyApproved_ShouldThrow()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.ApproveDeliveryAgentApplication();

        var act = () => user.SubmitDeliveryAgentApplication(VehicleType.Car);

        Assert.Throws<AgentApplicationNotPendingException>(act);
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Bike, user.VehicleType);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_WhenRejected_ShouldResubmit()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.RejectDeliveryAgentApplication();

        user.SubmitDeliveryAgentApplication(VehicleType.Car);

        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Car, user.VehicleType);
    }

    [Fact]
    public void SubmitDeliveryAgentApplication_WhenPending_ShouldRefreshVehicle()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        user.SubmitDeliveryAgentApplication(VehicleType.Car);

        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Equal(VehicleType.Car, user.VehicleType);
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_ShouldSetApprovedAndOffline()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        user.ApproveDeliveryAgentApplication();

        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_WhenNotPending_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.ApproveDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(act);
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_WhenRejected_ShouldThrow()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.RejectDeliveryAgentApplication();

        var act = () => user.ApproveDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(act);
        Assert.Equal(AgentApprovalStatus.Rejected, user.AgentApprovalStatus);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
    }

    [Fact]
    public void ApproveDeliveryAgentApplication_WhenAlreadyApproved_ShouldThrowWithoutChangingState()
    {
        var user = CreateApprovedAgent();

        var act = () => user.ApproveDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(act);
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void RejectDeliveryAgentApplication_WhenNotPending_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.RejectDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(act);
    }

    [Fact]
    public void RejectDeliveryAgentApplication_ShouldSetRejected()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);

        user.RejectDeliveryAgentApplication();

        Assert.Equal(AgentApprovalStatus.Rejected, user.AgentApprovalStatus);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
    }

    [Fact]
    public void RejectDeliveryAgentApplication_WhenApproved_ShouldThrowWithoutChangingState()
    {
        var user = CreateApprovedAgent();

        var act = () => user.RejectDeliveryAgentApplication();

        Assert.Throws<AgentApplicationNotPendingException>(act);
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void GoOnline_FromOffline_ShouldSetAvailable()
    {
        var user = CreateApprovedAgent();

        user.GoOnline();

        Assert.Equal(DeliveryAgentStatus.Available, user.DeliveryAgentStatus);
    }

    [Fact]
    public void GoOnline_FromAvailable_ShouldStayAvailable()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.GoOnline();

        Assert.Equal(DeliveryAgentStatus.Available, user.DeliveryAgentStatus);
    }

    [Fact]
    public void GoOnline_FromSuspended_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.Suspend();

        var act = () => user.GoOnline();

        Assert.Throws<AgentNotAvailableException>(act);
    }

    [Fact]
    public void GoOnline_FromBusy_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.MarkBusy();

        var act = () => user.GoOnline();

        Assert.Throws<AgentNotAvailableException>(act);
    }

    [Fact]
    public void GoOffline_FromAvailable_ShouldSetOffline()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.GoOffline();

        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void GoOffline_FromOffline_ShouldStayOffline()
    {
        var user = CreateApprovedAgent();

        user.GoOffline();

        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public void GoOffline_FromBusy_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.MarkBusy();

        var act = () => user.GoOffline();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GoOffline_FromSuspended_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.Suspend();

        var act = () => user.GoOffline();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("suspended", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Suspend_FromOffline_ShouldSetSuspended()
    {
        var user = CreateApprovedAgent();

        user.Suspend();

        Assert.Equal(DeliveryAgentStatus.Suspended, user.DeliveryAgentStatus);
    }

    [Fact]
    public void Suspend_FromAvailable_ShouldSetSuspended()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.Suspend();

        Assert.Equal(DeliveryAgentStatus.Suspended, user.DeliveryAgentStatus);
    }

    [Fact]
    public void Suspend_FromSuspended_ShouldStaySuspended()
    {
        var user = CreateApprovedAgent();
        user.Suspend();

        user.Suspend();

        Assert.Equal(DeliveryAgentStatus.Suspended, user.DeliveryAgentStatus);
    }

    [Fact]
    public void Suspend_FromBusy_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.MarkBusy();

        var act = () => user.Suspend();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkBusy_FromAvailable_ShouldSetBusy()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        user.MarkBusy();

        Assert.Equal(DeliveryAgentStatus.Busy, user.DeliveryAgentStatus);
    }

    [Fact]
    public void MarkBusy_FromOffline_ShouldThrow()
    {
        var user = CreateApprovedAgent();

        var act = () => user.MarkBusy();

        Assert.Throws<AgentNotAvailableException>(act);
    }

    [Fact]
    public void MarkBusy_FromBusy_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.MarkBusy();

        var act = () => user.MarkBusy();

        Assert.Throws<AgentNotAvailableException>(act);
    }

    [Fact]
    public void MarkBusy_FromSuspended_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.Suspend();

        var act = () => user.MarkBusy();

        Assert.Throws<AgentNotAvailableException>(act);
    }

    [Fact]
    public void MarkAvailable_FromBusy_ShouldSetAvailable()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();
        user.MarkBusy();

        user.MarkAvailable();

        Assert.Equal(DeliveryAgentStatus.Available, user.DeliveryAgentStatus);
    }

    [Fact]
    public void MarkAvailable_FromOffline_ShouldThrow()
    {
        var user = CreateApprovedAgent();

        var act = () => user.MarkAvailable();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkAvailable_FromAvailable_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.GoOnline();

        var act = () => user.MarkAvailable();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkAvailable_FromSuspended_ShouldThrow()
    {
        var user = CreateApprovedAgent();
        user.Suspend();

        var act = () => user.MarkAvailable();

        var ex = Assert.Throws<InvalidDeliveryAgentStatusTransitionException>(act);
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InternalAgentTransitions_WithoutApprovedCapability_ShouldThrowAgentGuard()
    {
        var user = CreateRegisteredUser();

        Assert.Throws<DeliveryAgentNotInitializedException>(() => user.MarkBusy());
        Assert.Throws<DeliveryAgentNotInitializedException>(() => user.MarkAvailable());
        Assert.Null(user.DeliveryAgentStatus);
    }

    [Fact]
    public void IsAvailable_ShouldReturnTrueOnlyWhenAvailable()
    {
        var user = CreateApprovedAgent();

        Assert.False(user.IsAvailable());

        user.GoOnline();
        Assert.True(user.IsAvailable());

        user.MarkBusy();
        Assert.False(user.IsAvailable());

        user.MarkAvailable();
        Assert.True(user.IsAvailable());
    }

    [Fact]
    public void UpdateLocation_WhenAgentApproved_ShouldUpdate()
    {
        var user = CreateApprovedAgent();
        var location = new GeoLocation(30.0m, 31.0m);

        user.UpdateLocation(location);

        Assert.Equal(location, user.CurrentLocation);
    }

    [Fact]
    public void UpdateLocation_Null_ShouldThrow()
    {
        var user = CreateApprovedAgent();

        var act = () => user.UpdateLocation(null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void UpdateLocation_WithoutAgentFlag_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.UpdateLocation(new GeoLocation(30.0m, 31.0m));

        Assert.Throws<DeliveryAgentNotInitializedException>(act);
    }

    [Fact]
    public void GoOnline_WithoutAgentFlag_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.GoOnline();

        Assert.Throws<DeliveryAgentNotInitializedException>(act);
    }

    [Fact]
    public void GoOffline_WithoutAgentFlag_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.GoOffline();

        Assert.Throws<DeliveryAgentNotInitializedException>(act);
    }

    [Fact]
    public void Suspend_WithoutAgentFlag_ShouldThrow()
    {
        var user = CreateRegisteredUser();

        var act = () => user.Suspend();

        Assert.Throws<DeliveryAgentNotInitializedException>(act);
    }

    [Fact]
    public void DualCapability_CustomerAndAgent_ShouldPreserveBoth()
    {
        var user = CreateRegisteredUser();
        user.InitializeCustomerProfile("John Customer", 25, "123");
        user.AddAddress(new Address("Street", "City", "123"));

        user.SubmitDeliveryAgentApplication(VehicleType.Car);
        user.ApproveDeliveryAgentApplication();

        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Single(user.Addresses);
        Assert.Equal("John Customer", user.FullName);
        Assert.Equal(25, user.Age);
        Assert.Equal("123", user.PhoneNumber);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.Equal(VehicleType.Car, user.VehicleType);
    }

    [Fact]
    public void GoOffline_FromOffline_ShouldNotThrow()
    {
        var user = CreateApprovedAgent();

        var act = () => user.GoOffline();

        act();
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    private static User CreateRegisteredUser()
    {
        return User.Register("agent1", "agent1@example.com", "Agent One");
    }

    private static User CreateApprovedAgent()
    {
        var user = CreateRegisteredUser();
        user.SubmitDeliveryAgentApplication(VehicleType.Bike);
        user.ApproveDeliveryAgentApplication();
        return user;
    }
}
