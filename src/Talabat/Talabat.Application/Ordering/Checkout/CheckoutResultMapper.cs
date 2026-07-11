using Talabat.Domain.DomainServices.Checkout;

namespace Talabat.Application.Ordering.Checkout;

public static class CheckoutResultMapper
{
    public static CheckoutOutcome ToOutcome(int orderId, CheckoutSucceeded checkoutSucceeded)
    {
        ArgumentNullException.ThrowIfNull(checkoutSucceeded);

        var total = checkoutSucceeded.Items
            .Select(item => item.UnitPrice.Multiply(item.Quantity))
            .Aggregate((left, right) => left.Add(right));

        return new CheckoutSucceededOutcome(orderId, total);
    }

    public static CheckoutOutcome ToOutcome(CheckoutProductsUnavailable unavailable)
    {
        ArgumentNullException.ThrowIfNull(unavailable);

        return new CheckoutProductsUnavailableOutcome(
            unavailable.UnavailableItems.Select(item => new UnavailableCheckoutItemOutcome(
                item.ProductId,
                item.ProductName,
                item.Reason)));
    }
}
