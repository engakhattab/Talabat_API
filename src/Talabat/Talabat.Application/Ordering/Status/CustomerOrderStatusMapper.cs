using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Ordering.Status;

/// <summary>
/// The only place that turns an order's fulfillment and delivery into the
/// <see cref="CustomerOrderStatus"/> customers see (design D9).
/// </summary>
public static class CustomerOrderStatusMapper
{
    public static CustomerOrderProgress Map(OrderFulfillment fulfillment, Delivery? delivery)
    {
        ArgumentNullException.ThrowIfNull(fulfillment);

        return (fulfillment.Status, delivery) switch
        {
            (OrderFulfillmentStatus.AwaitingRestaurant, null) =>
                new CustomerOrderProgress(CustomerOrderStatus.Placed, DeliveryAgentAssigned: false),
            (OrderFulfillmentStatus.Rejected, null) =>
                new CustomerOrderProgress(CustomerOrderStatus.Rejected, DeliveryAgentAssigned: false),
            (OrderFulfillmentStatus.Cancelled, null) =>
                new CustomerOrderProgress(CustomerOrderStatus.Cancelled, DeliveryAgentAssigned: false),
            (OrderFulfillmentStatus.Accepted, { } beforePickup) when IsBeforePickup(beforePickup) =>
                new CustomerOrderProgress(CustomerOrderStatus.Accepted, IsAgentAssigned(beforePickup)),
            (OrderFulfillmentStatus.Preparing, { } beforePickup) when IsBeforePickup(beforePickup) =>
                new CustomerOrderProgress(CustomerOrderStatus.Preparing, IsAgentAssigned(beforePickup)),
            (OrderFulfillmentStatus.ReadyForPickup, { } beforePickup) when IsBeforePickup(beforePickup) =>
                new CustomerOrderProgress(CustomerOrderStatus.ReadyForPickup, IsAgentAssigned(beforePickup)),
            (OrderFulfillmentStatus.HandedOver, { Status: DeliveryStatus.PickedUp or DeliveryStatus.OutForDelivery }) =>
                new CustomerOrderProgress(CustomerOrderStatus.OnTheWay, DeliveryAgentAssigned: true),
            (OrderFulfillmentStatus.HandedOver, { Status: DeliveryStatus.Delivered }) =>
                new CustomerOrderProgress(CustomerOrderStatus.Delivered, DeliveryAgentAssigned: true),
            _ => throw new InvalidOperationException(
                $"Order {fulfillment.OrderId} has fulfillment {fulfillment.Status} with "
                + $"{(delivery is null ? "no delivery" : $"delivery {delivery.Status}")}, which no lifecycle allows.")
        };
    }

    private static bool IsBeforePickup(Delivery delivery) =>
        delivery.Status is DeliveryStatus.PendingAssignment
            or DeliveryStatus.Assigned
            or DeliveryStatus.ArrivedAtRestaurant;

    private static bool IsAgentAssigned(Delivery delivery) => delivery.AssignedAgentId is not null;
}
