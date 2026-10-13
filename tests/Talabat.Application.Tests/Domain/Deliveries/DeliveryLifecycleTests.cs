using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ProgressOutForDelivery;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.Deliveries;

public sealed class DeliveryLifecycleTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Handle_OutForDelivery_TransitionsFromPickedUp()
    {
        var delivery = CreatePickedUpDelivery(1, 10);
        var (handler, clock) = CreateHandler(10, delivery);

        clock.UtcNow = Now.AddMinutes(10);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(DeliveryStatus.OutForDelivery, delivery.Status);
        Assert.Equal(clock.UtcNow, delivery.OutForDeliveryAt);
    }

    [Fact]
    public async Task Handle_OutForDelivery_SetsCorrectTimestamp()
    {
        var delivery = CreatePickedUpDelivery(1, 10);
        var (handler, clock) = CreateHandler(10, delivery);
        var expectedTime = new DateTime(2026, 7, 21, 15, 30, 0, DateTimeKind.Utc);

        clock.UtcNow = expectedTime;

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTime, delivery.OutForDeliveryAt);
    }

    [Fact]
    public async Task Handle_OutForDelivery_FromAssignedStatus_ThrowsInvalidTransition()
    {
        var delivery = CreateAssignedDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(InvalidDeliveryStatusTransitionException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_FromArrivedAtRestaurant_ThrowsInvalidTransition()
    {
        var delivery = CreateArrivedAtRestaurantDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(InvalidDeliveryStatusTransitionException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_FromDelivered_ThrowsTerminalState()
    {
        var delivery = CreateDeliveredDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryTerminalStateException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_WrongAgent_ReturnsNotFound()
    {
        var delivery = CreatePickedUpDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 99));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_DeliveryNotFound_ReturnsNotFound()
    {
        var (handler, _) = CreateHandler(10);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 999, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_FromPendingAssignment_ReturnsNotFound()
    {
        var delivery = CreatePendingDelivery(1);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_OutForDelivery_CompleteLifecycle_OutForDeliveryIsStep4()
    {
        var delivery = CreatePendingDelivery(1);

        delivery.AssignAgent(10, Now.AddMinutes(1));
        delivery.MarkArrivedAtRestaurant(10, Now.AddMinutes(2));
        delivery.MarkPickedUp(10, Now.AddMinutes(3));

        var clock = new FakeClock { UtcNow = Now.AddMinutes(4) };
        var deliveryRepository = new FakeDeliveryRepository();
        deliveryRepository.Deliveries.Add(delivery);
        var handler = new OutForDeliveryHandler(
            deliveryRepository,
            new FakeUnitOfWork(),
            clock);

        var result = await handler.Handle(new OutForDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryStatus.OutForDelivery, delivery.Status);
        Assert.Equal(Now.AddMinutes(4), delivery.OutForDeliveryAt);
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
        var delivery = new Delivery(1, 1, 1, "Test Restaurant", Address, Address, Now);
        TestIds.SetId(delivery, id);
        return delivery;
    }

    private static Delivery CreateAssignedDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        return delivery;
    }

    private static Delivery CreateArrivedAtRestaurantDelivery(int id, int agentId)
    {
        var delivery = CreateAssignedDelivery(id, agentId);
        delivery.MarkArrivedAtRestaurant(agentId, Now.AddMinutes(2));
        return delivery;
    }

    private static Delivery CreatePickedUpDelivery(int id, int agentId)
    {
        var delivery = CreateArrivedAtRestaurantDelivery(id, agentId);
        delivery.MarkPickedUp(agentId, Now.AddMinutes(3));
        return delivery;
    }

    private static Delivery CreateDeliveredDelivery(int id, int agentId)
    {
        var delivery = CreatePickedUpDelivery(id, agentId);
        delivery.MarkOutForDelivery(agentId, Now.AddMinutes(4));
        delivery.MarkDelivered(agentId, Now.AddMinutes(5));
        return delivery;
    }

    private static (OutForDeliveryHandler Handler, FakeClock Clock) CreateHandler(
        int agentId,
        params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var clock = new FakeClock { UtcNow = Now };

        var handler = new OutForDeliveryHandler(
            deliveryRepository,
            new FakeUnitOfWork(),
            clock);

        return (handler, clock);
    }
}
