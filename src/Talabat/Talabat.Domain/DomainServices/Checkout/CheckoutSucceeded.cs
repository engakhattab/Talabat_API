using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.DomainServices.Checkout;

public sealed class CheckoutSucceeded : CheckoutResult
{
    public IReadOnlyCollection<CheckoutItemSnapshot> Items { get; }

    public CheckoutSucceeded(IEnumerable<CheckoutItemSnapshot> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();

        if (itemList.Count == 0)
        {
            throw new ArgumentException(
                "Checkout items cannot be empty.",
                nameof(items));
        }

        if (itemList.Any(item => item is null))
        {
            throw new ArgumentException(
                "Checkout items cannot contain null values.",
                nameof(items));
        }

        Items = itemList.AsReadOnly();
    }
}
