using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Fulfillment;

/// <summary>
/// Who drives a fulfillment transition (design D11). The Admin API acts on behalf of the order's own restaurant;
/// the Owner API (M3) passes its restaurant, so the same use case refuses another restaurant's orders.
/// </summary>
public abstract record FulfillmentActor
{
    private FulfillmentActor()
    {
    }

    public sealed record Admin : FulfillmentActor;

    public sealed record Restaurant(int RestaurantId) : FulfillmentActor;

    /// <summary>
    /// The restaurant the transition is performed as: an Admin acts as the order's own restaurant, a restaurant as
    /// itself (so the domain's RestaurantId guard refuses another restaurant's order).
    /// </summary>
    public int ActingRestaurantId(OrderFulfillment fulfillment)
    {
        ArgumentNullException.ThrowIfNull(fulfillment);

        return this switch
        {
            Restaurant restaurant => restaurant.RestaurantId,
            _ => fulfillment.RestaurantId
        };
    }
}
