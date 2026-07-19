using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.DeliveryDomain;

public sealed class DeliveryAssignmentDomainServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public void Assign_MakesAgentBusy_SetsDeliveryAssigned()
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
    public void Assign_NonAvailableAgent_ThrowsAgentNotAvailable()
    {
        var agent = CreateOfflineAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<AgentNotAvailableException>(() => service.Assign(delivery, agent, Now));
    }

    [Fact]
    public void CompleteDelivery_MarksDelivered_AgentAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateOutForDeliveryDelivery(1, 10);
        var service = new DeliveryAssignmentDomainService();

        service.CompleteDelivery(delivery, agent, Now.AddMinutes(5));

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void CancelDelivery_Cancels_AgentAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        delivery.MarkArrivedAtRestaurant(agent.Id, Now.AddMinutes(1));
        var service = new DeliveryAssignmentDomainService();

        service.CancelDelivery(delivery, agent, Now.AddMinutes(5));

        Assert.Equal(DeliveryStatus.Cancelled, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void FailDelivery_Fails_AgentAvailable()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        delivery.MarkArrivedAtRestaurant(agent.Id, Now.AddMinutes(1));
        delivery.MarkPickedUp(agent.Id, Now.AddMinutes(2));
        delivery.MarkOutForDelivery(agent.Id, Now.AddMinutes(3));
        var service = new DeliveryAssignmentDomainService();

        service.FailDelivery(delivery, agent, "Vehicle broke down", Now.AddMinutes(5));

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("Vehicle broke down", delivery.FailureReason);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public void CompleteDelivery_MismatchedAgent_ThrowsAgentMismatch()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 99);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<DeliveryAgentMismatchException>(() =>
            service.CompleteDelivery(delivery, agent, Now));
    }

    [Fact]
    public void CompleteDelivery_UnassignedDelivery_ThrowsDeliveryNotAssigned()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<DeliveryNotAssignedException>(() =>
            service.CompleteDelivery(delivery, agent, Now));
    }

    [Fact]
    public void CompleteDelivery_NonBusyAgent_ThrowsAgentNotAvailable()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var service = new DeliveryAssignmentDomainService();

        Assert.Throws<AgentNotAvailableException>(() =>
            service.CompleteDelivery(delivery, agent, Now));
    }

    [Fact]
    public void Assign_PreservesAssignedAgentId()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var service = new DeliveryAssignmentDomainService();

        service.Assign(delivery, agent, Now);

        Assert.Equal(10, delivery.AssignedAgentId);
        Assert.Equal(10, agent.Id);
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
