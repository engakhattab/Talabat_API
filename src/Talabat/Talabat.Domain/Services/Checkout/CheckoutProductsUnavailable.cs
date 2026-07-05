namespace Talabat.Domain.Services.Checkout;

public sealed class CheckoutProductsUnavailable : CheckoutResult
{
    public IReadOnlyCollection<UnavailableCheckoutItem> UnavailableItems { get; }

    public CheckoutProductsUnavailable(IEnumerable<UnavailableCheckoutItem> unavailableItems)
    {
        ArgumentNullException.ThrowIfNull(unavailableItems);

        var itemList = unavailableItems.ToList();

        if (itemList.Count == 0)
        {
            throw new ArgumentException(
                "Unavailable checkout items cannot be empty.",
                nameof(unavailableItems));
        }

        UnavailableItems = itemList.AsReadOnly();
    }
}
