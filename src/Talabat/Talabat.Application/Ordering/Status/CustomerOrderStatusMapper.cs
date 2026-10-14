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
            _ => throw new InvalidOperationException(
                $"Order {fulfillment.OrderId} has fulfillment {fulfillment.Status} with "
                + $"{(delivery is null ? "no delivery" : $"delivery {delivery.Status}")}, which no lifecycle allows.")
        };
    }
}
