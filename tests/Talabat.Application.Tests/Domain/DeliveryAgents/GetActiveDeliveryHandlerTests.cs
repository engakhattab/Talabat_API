using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.GetActiveDelivery;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class GetActiveDeliveryHandlerTests
{
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_GetActiveDelivery_WhenActiveDeliveryExists_ShouldReturnDto()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var handler = CreateHandler(agent, delivery);

        var result = await handler.Handle(new GetActiveDeliveryQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Id);
        Assert.Equal(DeliveryStatus.Assigned, result.Value.Status);
        Assert.Equal("Street", result.Value.Street);
    }

    [Fact]
    public async Task Handle_GetActiveDelivery_WhenNoActiveDelivery_ShouldReturnNotFound()
    {
        var agent = CreateAvailableAgent(10);
        var handler = CreateHandler(agent);

        var result = await handler.Handle(new GetActiveDeliveryQuery());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GetActiveDelivery_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new GetActiveDeliveryQuery());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GetActiveDelivery_WrongAgent_ShouldReturnNotFound()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreateAssignedDelivery(1, 99);
        var handler = CreateHandler(agent, delivery);

        var result = await handler.Handle(new GetActiveDeliveryQuery());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
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

    private static GetActiveDeliveryHandler CreateHandler(User agent, params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = agent.Id,
            HasDeliveryAgentCapability = true,
            AgentId = agent.Id
        };

        return new GetActiveDeliveryHandler(deliveryRepository, currentUser);
    }

    private static GetActiveDeliveryHandler CreateHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new GetActiveDeliveryHandler(
            new FakeDeliveryRepository(),
            currentUser);
    }
}
