using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ProgressPickup;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class PickUpOrderHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Handle_PickUpOrder_FromArrivedAtRestaurant_ShouldSucceed()
    {
        var delivery = CreateArrivedAtRestaurantDelivery(1, 10);
        var (handler, clock) = CreateHandler(10, delivery);

        clock.UtcNow = Now.AddMinutes(5);

        var result = await handler.Handle(new PickUpOrderCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(DeliveryStatus.PickedUp, delivery.Status);
        Assert.Equal(clock.UtcNow, delivery.PickedUpAt);
    }

    [Fact]
    public async Task Handle_PickUpOrder_WrongAgent_ShouldReturnNotFound()
    {
        var delivery = CreateArrivedAtRestaurantDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new PickUpOrderCommand(DeliveryId: 1, AgentId: 99));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_PickUpOrder_DeliveryNotFound_ShouldReturnNotFound()
    {
        var (handler, _) = CreateHandler(10);

        var result = await handler.Handle(new PickUpOrderCommand(DeliveryId: 999, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_PickUpOrder_FromAssigned_ShouldFail()
    {
        var delivery = CreateAssignedDelivery(1, 10);
        var (handler, _) = CreateHandler(10, delivery);

        var result = await handler.Handle(new PickUpOrderCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(InvalidDeliveryStatusTransitionException), result.Error?.Code);
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

    private static (PickUpOrderHandler Handler, FakeClock Clock) CreateHandler(
        int agentId,
        params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var clock = new FakeClock { UtcNow = Now };

        var handler = new PickUpOrderHandler(
            deliveryRepository,
            new FakeUnitOfWork(),
            clock);

        return (handler, clock);
    }
}
