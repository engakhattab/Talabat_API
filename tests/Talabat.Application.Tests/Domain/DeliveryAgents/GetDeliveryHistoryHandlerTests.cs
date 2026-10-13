using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.GetDeliveryHistory;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class GetDeliveryHistoryHandlerTests
{
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_GetDeliveryHistory_WhenHistoryExists_ShouldReturnList()
    {
        var agent = CreateAvailableAgent(10);
        var delivered = CreateDeliveredDelivery(1, 10);
        var assigned = CreateAssignedDelivery(2, 10);
        var handler = CreateHandler(delivered, assigned);

        var result = await handler.Handle(new GetDeliveryHistoryQuery(agent.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Null(typeof(DeliveryHistoryDto).GetProperty("CustomerId"));
        Assert.Null(typeof(DeliveryHistoryDto).GetProperty("CustomerPhoneNumber"));
    }

    [Fact]
    public async Task Handle_GetDeliveryHistory_WhenNoHistory_ShouldReturnEmptyList()
    {
        var agent = CreateAvailableAgent(10);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDeliveryHistoryQuery(agent.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
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

    private static Delivery CreateDeliveredDelivery(int id, int agentId)
    {
        var delivery = CreateAssignedDelivery(id, agentId);
        delivery.MarkArrivedAtRestaurant(agentId, Now.AddMinutes(2));
        delivery.MarkPickedUp(agentId, Now.AddMinutes(3));
        delivery.MarkOutForDelivery(agentId, Now.AddMinutes(4));
        delivery.MarkDelivered(agentId, Now.AddMinutes(5));
        return delivery;
    }

    private static GetDeliveryHistoryHandler CreateHandler(params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        return new GetDeliveryHistoryHandler(deliveryRepository);
    }
}
