using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.GetPendingDeliveries;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class GetPendingDeliveriesHandlerTests
{
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_GetPendingDeliveries_WhenPendingExist_ShouldReturnList()
    {
        var delivery1 = CreatePendingDelivery(1);
        var delivery2 = CreatePendingDelivery(2);
        var handler = CreateHandler(delivery1, delivery2);

        var result = await handler.Handle(new GetPendingDeliveriesQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task Handle_GetPendingDeliveries_WhenNonePending_ShouldReturnEmptyList()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetPendingDeliveriesQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_GetPendingDeliveries_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new GetPendingDeliveriesQuery());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    private static Delivery CreatePendingDelivery(int id)
    {
        var delivery = new Delivery(1, 1, 1, Address, Now);
        TestIds.SetId(delivery, id);
        return delivery;
    }

    private static GetPendingDeliveriesHandler CreateHandler(params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 1,
            HasDeliveryAgentCapability = true,
            AgentId = 1
        };

        return new GetPendingDeliveriesHandler(deliveryRepository, currentUser);
    }

    private static GetPendingDeliveriesHandler CreateHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new GetPendingDeliveriesHandler(
            new FakeDeliveryRepository(),
            currentUser);
    }
}
