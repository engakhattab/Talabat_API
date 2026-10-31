using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Tests.DeliveryManagement;

public sealed class DeliveryReleaseTests
{
    private const int AgentId = 10;

    private static readonly DateTime CreatedAt =
        new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    // Design §4 test 12 (D6).
    [Theory]
    [InlineData(DeliveryStatus.Assigned)]
    [InlineData(DeliveryStatus.ArrivedAtRestaurant)]
    public void Release_from_assigned_or_arrived_returns_to_pending_and_clears_assignment(DeliveryStatus from)
    {
        var delivery = CreateDeliveryIn(from);

        delivery.Release(AgentId, CreatedAt.AddMinutes(10));

        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.AssignedAgentId);
        Assert.Null(delivery.AssignedAt);
        Assert.Null(delivery.ArrivedAtRestaurantAt);
        Assert.False(delivery.IsActive());
    }

    // D6: the same delivery row is reused, so another agent can claim it.
    [Fact]
    public void Released_delivery_can_be_claimed_by_another_agent()
    {
        var delivery = CreateDeliveryIn(DeliveryStatus.ArrivedAtRestaurant);
        delivery.Release(AgentId, CreatedAt.AddMinutes(10));

        delivery.AssignAgent(20, CreatedAt.AddMinutes(11));

        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(20, delivery.AssignedAgentId);
        Assert.Equal(CreatedAt.AddMinutes(11), delivery.AssignedAt);
    }

    // Design §4 test 13 (D6): once the food is with the agent, release is not the way out (fail is, D8).
    [Theory]
    [InlineData(DeliveryStatus.PickedUp)]
    [InlineData(DeliveryStatus.OutForDelivery)]
    public void Release_after_pickup_throws(DeliveryStatus from)
    {
        var delivery = CreateDeliveryIn(from);

        Assert.Throws<InvalidDeliveryStatusTransitionException>(
            () => delivery.Release(AgentId, CreatedAt.AddMinutes(10)));
        Assert.Equal(from, delivery.Status);
        Assert.Equal(AgentId, delivery.AssignedAgentId);
    }

    // Design §4 test 13 (D6).
    [Fact]
    public void Release_by_other_agent_throws_mismatch()
    {
        var delivery = CreateDeliveryIn(DeliveryStatus.Assigned);

        Assert.Throws<DeliveryAgentMismatchException>(
            () => delivery.Release(99, CreatedAt.AddMinutes(10)));
        Assert.Equal(AgentId, delivery.AssignedAgentId);
    }

    [Fact]
    public void Release_of_unassigned_delivery_throws_not_assigned()
    {
        var delivery = new Delivery(300, 1, 1, "Cairo Grill", Address, Address, CreatedAt);

        Assert.Throws<DeliveryNotAssignedException>(
            () => delivery.Release(AgentId, CreatedAt.AddMinutes(10)));
    }

    private static Delivery CreateDeliveryIn(DeliveryStatus status)
    {
        var delivery = new Delivery(300, 1, 1, "Cairo Grill", Address, Address, CreatedAt);
        delivery.AssignAgent(AgentId, CreatedAt.AddMinutes(1));

        if (status == DeliveryStatus.Assigned)
        {
            return delivery;
        }

        delivery.MarkArrivedAtRestaurant(AgentId, CreatedAt.AddMinutes(2));

        if (status == DeliveryStatus.ArrivedAtRestaurant)
        {
            return delivery;
        }

        delivery.MarkPickedUp(AgentId, CreatedAt.AddMinutes(3));

        if (status == DeliveryStatus.PickedUp)
        {
            return delivery;
        }

        delivery.MarkOutForDelivery(AgentId, CreatedAt.AddMinutes(4));
        return delivery;
    }
}
