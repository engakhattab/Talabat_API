using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.DeliveryDomain;

public sealed class AgentAssignmentAuthorizationTests
{
    private static readonly DateTime Now = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public void Assign_CustomerOnlyUser_ThrowsDeliveryAgentNotInitialized()
    {
        var user = User.Register("cust1@test.com", "cust1@test.com", "Customer User");
        user.InitializeCustomerProfile("Customer User", 30, null);
        SetId(user, 10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<DeliveryAgentNotInitializedException>(() => service.Assign(delivery, user, Now));
    }

    [Fact]
    public void Assign_OfflineAgent_ThrowsAgentNotAvailable()
    {
        var agent = CreateOfflineAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<AgentNotAvailableException>(() => service.Assign(delivery, agent, Now));
    }

    [Fact]
    public void Assign_SuspendedAgent_ThrowsAgentNotAvailable()
    {
        var agent = CreateSuspendedAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<AgentNotAvailableException>(() => service.Assign(delivery, agent, Now));
    }

    [Fact]
    public void Assign_BusyAgent_ThrowsAgentNotAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<AgentNotAvailableException>(() => service.Assign(delivery, agent, Now));
    }

    [Fact]
    public void Assign_AvailableAgent_MarksBusy_SetsDeliveryAssigned()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        service.Assign(delivery, agent, Now);

        Assert.Equal(agent.Id, delivery.AssignedAgentId);
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Busy, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void Assign_AvailableAgent_PreservesAssignedAgentId()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        service.Assign(delivery, agent, Now);

        Assert.Equal(10, delivery.AssignedAgentId);
    }

    [Fact]
    public void CompleteDelivery_BusyAgent_ReleasesToAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateOutForDeliveryDelivery(1, 10);
        var service = new DeliveryAssignmentDomainService();

        service.CompleteDelivery(delivery, agent, Now.AddMinutes(5));

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void CancelDelivery_BusyAgent_ReleasesToAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        delivery.MarkArrivedAtRestaurant(10, Now.AddMinutes(1));
        var service = new DeliveryAssignmentDomainService();

        service.CancelDelivery(delivery, agent, Now.AddMinutes(5));

        Assert.Equal(DeliveryStatus.Cancelled, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void CompleteDelivery_UnassignedDelivery_ThrowsDeliveryNotAssigned()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<DeliveryNotAssignedException>(() => service.CompleteDelivery(delivery, agent, Now));
    }

    [Fact]
    public void CompleteDelivery_MismatchedAgent_ThrowsDeliveryAgentMismatch()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 99);
        delivery.MarkArrivedAtRestaurant(99, Now.AddMinutes(1));
        delivery.MarkPickedUp(99, Now.AddMinutes(2));
        delivery.MarkOutForDelivery(99, Now.AddMinutes(3));
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<DeliveryAgentMismatchException>(() => service.CompleteDelivery(delivery, agent, Now));
    }

    private static User CreateAvailableAgent(int id)
    {
        var agent = User.Register($"agent{id}@test.com", $"agent{id}@test.com", $"Agent {id}");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        agent.GoOnline();
        agent.UpdateLocation(new GeoLocation(30.0m, 31.0m));
        SetId(agent, id);
        return agent;
    }

    private static User CreateBusyAgent(int id)
    {
        var agent = CreateAvailableAgent(id);
        agent.MarkBusy();
        return agent;
    }

    private static User CreateOfflineAgent(int id)
    {
        var agent = User.Register($"agent{id}@test.com", $"agent{id}@test.com", $"Agent {id}");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        SetId(agent, id);
        return agent;
    }

    private static User CreateSuspendedAgent(int id)
    {
        var agent = CreateOfflineAgent(id);
        agent.GoOnline();
        agent.Suspend();
        return agent;
    }

    private static Delivery CreatePendingDelivery(int id)
    {
        var delivery = new Delivery(1, 1, 1, Address, Now);
        SetDeliveryId(delivery, id);
        return delivery;
    }

    private static Delivery CreateAssignedDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        return delivery;
    }

    private static Delivery CreateOutForDeliveryDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        delivery.MarkArrivedAtRestaurant(agentId, Now.AddMinutes(2));
        delivery.MarkPickedUp(agentId, Now.AddMinutes(3));
        delivery.MarkOutForDelivery(agentId, Now.AddMinutes(4));
        return delivery;
    }

    private static void SetId(User user, int id)
    {
        var prop = typeof(User).GetProperty(nameof(User.Id))
            ?? throw new InvalidOperationException("User.Id property not found");
        prop.SetValue(user, id);
    }

    private static void SetDeliveryId(Delivery delivery, int id)
    {
        var prop = typeof(Delivery).GetProperty(nameof(Delivery.Id))
            ?? throw new InvalidOperationException("Delivery.Id property not found");
        prop.SetValue(delivery, id);
    }
}
