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
        throw new NotImplementedException();
    }
}
