using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ProgressRelease;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

/// <summary>Design §4 test 20 (D6): release by the agent, and by an Admin on their behalf.</summary>
public sealed class ReleaseDeliveryHandlerTests
{
    private const int AgentId = 10;
    private const int OrderId = 300;
    private const int DeliveryId = 1;

    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Release_returns_agent_to_available()
    {
        var fixture = new Fixture();
        var delivery = fixture.AddAssignedDelivery(arrived: true);

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAgent(DeliveryId, AgentId));

        Assert.True(result.IsSuccess);
        Assert.Equal(new ReleasedDelivery(DeliveryId, OrderId, AgentId), result.Value);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.AssignedAgentId);
        Assert.Equal(DeliveryAgentStatus.Available, fixture.Agent.DeliveryAgentStatus);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Admin_release_returns_agent_to_available()
    {
        var fixture = new Fixture();
        var delivery = fixture.AddAssignedDelivery(arrived: false);

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAdmin(OrderId));

        Assert.True(result.IsSuccess);
        Assert.Equal(new ReleasedDelivery(DeliveryId, OrderId, AgentId), result.Value);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Equal(DeliveryAgentStatus.Available, fixture.Agent.DeliveryAgentStatus);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Release_by_other_agent_returns_not_found()
    {
        var fixture = new Fixture();
        var delivery = fixture.AddAssignedDelivery(arrived: false);

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAgent(DeliveryId, AgentId: 20));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error.Code);
        Assert.Equal(AgentId, delivery.AssignedAgentId);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Release_after_pickup_conflicts_and_keeps_agent_busy()
    {
        var fixture = new Fixture();
        var delivery = fixture.AddAssignedDelivery(arrived: true);
        delivery.MarkPickedUp(AgentId, Now.AddMinutes(3));

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAgent(DeliveryId, AgentId));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Conflict, result.Error!.Category);
        Assert.Equal(nameof(InvalidDeliveryStatusTransitionException), result.Error.Code);
        Assert.Equal(DeliveryAgentStatus.Busy, fixture.Agent.DeliveryAgentStatus);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Admin_release_of_unassigned_delivery_conflicts()
    {
        var fixture = new Fixture();
        fixture.AddPendingDelivery();

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAdmin(OrderId));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Conflict, result.Error!.Category);
        Assert.Equal(nameof(DeliveryNotAssignedException), result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Admin_release_of_order_without_delivery_returns_not_found()
    {
        var fixture = new Fixture();

        var result = await fixture.Handler.Handle(new ReleaseDeliveryCommand.ByAdmin(OrderId));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error.Code);
    }

    private sealed class Fixture
    {
        private readonly FakeDeliveryRepository _deliveries = new();
        private readonly FakeUserRepository _users = new();

        public Fixture()
        {
            Agent = CreateAvailableAgent(AgentId);
            _users.Users.Add(Agent);
            UnitOfWork = new FakeUnitOfWork();
            Handler = new ReleaseDeliveryHandler(
                _deliveries,
                _users,
                new DeliveryAssignmentDomainService(),
                UnitOfWork,
                new FakeClock { UtcNow = Now.AddMinutes(10) });
        }

        public User Agent { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public ReleaseDeliveryHandler Handler { get; }

        public Delivery AddPendingDelivery()
        {
            var delivery = new Delivery(OrderId, 1, 1, "Cairo Grill", Address, Address, Now);
            TestIds.SetId(delivery, DeliveryId);
            _deliveries.Deliveries.Add(delivery);
            return delivery;
        }

        public Delivery AddAssignedDelivery(bool arrived)
        {
            var delivery = AddPendingDelivery();
            new DeliveryAssignmentDomainService().Assign(delivery, Agent, Now.AddMinutes(1));

            if (arrived)
            {
                delivery.MarkArrivedAtRestaurant(AgentId, Now.AddMinutes(2));
            }

            return delivery;
        }

        private static User CreateAvailableAgent(int id)
        {
            var agent = User.Register($"agent{id}@test.com", $"agent{id}@test.com", $"Agent {id}");
            agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
            agent.ApproveDeliveryAgentApplication();
            agent.GoOnline();
            agent.UpdateLocation(new GeoLocation(30.0m, 31.0m));
            TestIds.SetId(agent, id);
            return agent;
        }
    }
}
