using Talabat.Domain.Common;

namespace Talabat.Domain.DomainServices.Checkout;

public sealed class UnavailableCheckoutItem
{
    public int ProductId { get; }

    public string ProductName { get; }

    public string Reason { get; }

    public UnavailableCheckoutItem(int productId, string productName, string reason)
    {
        ProductId = Guard.Positive(productId, nameof(productId));
        ProductName = Guard.RequiredText(productName, nameof(productName));
        Reason = Guard.RequiredText(reason, nameof(reason));
    }
}
