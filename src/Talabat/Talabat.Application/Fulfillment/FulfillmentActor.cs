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
}
