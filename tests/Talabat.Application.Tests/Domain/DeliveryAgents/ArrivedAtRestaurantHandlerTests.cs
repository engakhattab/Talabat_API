using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ProgressArrive;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class ArrivedAtRestaurantHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_FromAssigned_ShouldSucceed()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var (handler, clock) = CreateHandler(agent, delivery);

        clock.UtcNow = Now.AddMinutes(5);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(DeliveryStatus.ArrivedAtRestaurant, delivery.Status);
        Assert.Equal(clock.UtcNow, delivery.ArrivedAtRestaurantAt);
    }

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_WrongAgent_ShouldFail()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateAssignedDelivery(1, 99);
        var (handler, _) = CreateHandler(agent, delivery);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryAgentMismatchException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_DeliveryNotFound_ShouldReturnNotFound()
    {
        var agent = CreateAvailableAgent(10);
        var (handler, _) = CreateHandler(agent);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_FromPendingAssignment_ShouldFail()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var (handler, _) = CreateHandler(agent, delivery);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryNotAssignedException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ArrivedAtRestaurant_FromDelivered_ShouldFail()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateDeliveredDelivery(1, 10);
        var (handler, _) = CreateHandler(agent, delivery);

        var result = await handler.Handle(new ArrivedAtRestaurantCommand(DeliveryId: 1));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryTerminalStateException), result.Error?.Code);
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

    private static Delivery CreatePendingDelivery(int id)
    {
        var delivery = new Delivery(1, 1, 1, Address, Now);
        TestIds.SetId(delivery, id);
        return delivery;
    }

    private static Delivery CreateAssignedDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        return delivery;
    }

    private static Delivery CreateDeliveredDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        delivery.MarkArrivedAtRestaurant(agentId, Now.AddMinutes(2));
        delivery.MarkPickedUp(agentId, Now.AddMinutes(3));
        delivery.MarkOutForDelivery(agentId, Now.AddMinutes(4));
        delivery.MarkDelivered(agentId, Now.AddMinutes(5));
        return delivery;
    }

    private static (ArrivedAtRestaurantHandler Handler, FakeClock Clock) CreateHandler(
        User agent,
        params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var clock = new FakeClock { UtcNow = Now };
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = agent.Id,
            HasDeliveryAgentCapability = true,
            AgentId = agent.Id
        };

        var handler = new ArrivedAtRestaurantHandler(
            deliveryRepository,
            new FakeUnitOfWork(),
            clock,
            currentUser);

        return (handler, clock);
    }

    private static ArrivedAtRestaurantHandler CreateHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new ArrivedAtRestaurantHandler(
            new FakeDeliveryRepository(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }
}
