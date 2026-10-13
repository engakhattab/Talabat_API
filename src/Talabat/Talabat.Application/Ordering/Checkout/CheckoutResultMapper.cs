using Talabat.Domain.DomainServices.Checkout;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Ordering.Checkout;

public static class CheckoutResultMapper
{
    public static CheckoutOutcome ToOutcome(
        int orderId,
        CheckoutSucceeded checkoutSucceeded,
        int restaurantId,
        DeliveryAddressSnapshot deliveryAddress)
    {
        ArgumentNullException.ThrowIfNull(checkoutSucceeded);

        var total = checkoutSucceeded.Items
            .Select(item => item.UnitPrice.Multiply(item.Quantity))
            .Aggregate((left, right) => left.Add(right));

        return new CheckoutSucceededOutcome(orderId, total, restaurantId, deliveryAddress);
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
