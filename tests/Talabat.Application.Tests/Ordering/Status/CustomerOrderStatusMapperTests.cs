using Talabat.Application.Ordering.Status;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Ordering.Status;

/// <summary>
/// Design §4 test 25 (D9). Each Phase 2 slice adds the combinations it makes reachable; T9 completes
/// the enumeration.
/// </summary>
public sealed class CustomerOrderStatusMapperTests
{
    private const int OrderId = 300;
    private const int RestaurantId = 1;
    private const int AgentId = 42;

    [Theory]
    [InlineData(OrderFulfillmentStatus.AwaitingRestaurant, null, CustomerOrderStatus.Placed, false)]
    [InlineData(OrderFulfillmentStatus.Accepted, DeliveryStatus.PendingAssignment, CustomerOrderStatus.Accepted, false)]
    [InlineData(OrderFulfillmentStatus.Accepted, DeliveryStatus.Assigned, CustomerOrderStatus.Accepted, true)]
    [InlineData(OrderFulfillmentStatus.Accepted, DeliveryStatus.ArrivedAtRestaurant, CustomerOrderStatus.Accepted, true)]
    public void Status_mapper_maps_every_reachable_combination(
        OrderFulfillmentStatus fulfillmentStatus,
        DeliveryStatus? deliveryStatus,
        CustomerOrderStatus expectedStatus,
        bool expectedDeliveryAgentAssigned)
    {
        var fulfillment = CreateFulfillment(fulfillmentStatus);
        var delivery = deliveryStatus is null ? null : CreateDelivery(deliveryStatus.Value);

        var progress = CustomerOrderStatusMapper.Map(fulfillment, delivery);

        Assert.Equal(expectedStatus, progress.Status);
        Assert.Equal(expectedDeliveryAgentAssigned, progress.DeliveryAgentAssigned);
    }

    [Theory]
    // A delivery exists only once the restaurant has accepted (D3).
    [InlineData(OrderFulfillmentStatus.AwaitingRestaurant, DeliveryStatus.PendingAssignment)]
    // Acceptance creates the delivery in the same transaction (D3).
    [InlineData(OrderFulfillmentStatus.Accepted, null)]
    // Food leaves the restaurant only through the hand-over (D5).
    [InlineData(OrderFulfillmentStatus.Accepted, DeliveryStatus.PickedUp)]
    public void Status_mapper_rejects_impossible_combinations(
        OrderFulfillmentStatus fulfillmentStatus,
        DeliveryStatus? deliveryStatus)
    {
        var fulfillment = CreateFulfillment(fulfillmentStatus);
        var delivery = deliveryStatus is null ? null : CreateDelivery(deliveryStatus.Value);

        Assert.Throws<InvalidOperationException>(() => CustomerOrderStatusMapper.Map(fulfillment, delivery));
    }

    private static OrderFulfillment CreateFulfillment(OrderFulfillmentStatus status)
    {
        var fulfillment = new OrderFulfillment(OrderId, RestaurantId, TestData.UtcNow, TimeSpan.FromMinutes(10));

        return status switch
        {
            OrderFulfillmentStatus.AwaitingRestaurant => fulfillment,
            OrderFulfillmentStatus.Accepted => Accepted(fulfillment),
            _ => throw new NotSupportedException($"Arrange a {status} fulfillment once that transition exists.")
        };
    }

    private static Delivery CreateDelivery(DeliveryStatus status)
    {
        var address = new DeliveryAddressSnapshot("Street", "Cairo", "10");
        var delivery = new Delivery(OrderId, 1, RestaurantId, "Restaurant 1", address, address, TestData.UtcNow);

        return status switch
        {
            DeliveryStatus.PendingAssignment => delivery,
            DeliveryStatus.Assigned => Assigned(delivery),
            DeliveryStatus.ArrivedAtRestaurant => ArrivedAtRestaurant(Assigned(delivery)),
            DeliveryStatus.PickedUp => PickedUp(ArrivedAtRestaurant(Assigned(delivery))),
            _ => throw new NotSupportedException($"Arrange a {status} delivery when a test needs it.")
        };
    }

    private static OrderFulfillment Accepted(OrderFulfillment fulfillment)
    {
        fulfillment.Accept(RestaurantId, TestData.UtcNow.AddMinutes(1));
        return fulfillment;
    }

    private static Delivery Assigned(Delivery delivery)
    {
        delivery.AssignAgent(AgentId, TestData.UtcNow.AddMinutes(2));
        return delivery;
    }

    private static Delivery ArrivedAtRestaurant(Delivery delivery)
    {
        delivery.MarkArrivedAtRestaurant(AgentId, TestData.UtcNow.AddMinutes(3));
        return delivery;
    }

    private static Delivery PickedUp(Delivery delivery)
    {
        delivery.MarkPickedUp(AgentId, TestData.UtcNow.AddMinutes(4));
        return delivery;
    }
}
