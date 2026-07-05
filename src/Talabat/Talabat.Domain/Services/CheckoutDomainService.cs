using Talabat.Domain.Basket;
using Talabat.Domain.Catalog;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Services.Checkout;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Services;

public sealed class CheckoutDomainService
{
    public CheckoutResult ValidateCheckout(
        Cart cart,
        Restaurant restaurant,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(restaurant);

        if (deliveryAddress is null)
        {
            throw new MissingDeliveryAddressException();
        }

        if (cart.Status != CartStatus.Active)
        {
            throw new CartNotActiveException();
        }

        if (cart.IsExpired(currentTime))
        {
            throw new CartExpiredException();
        }

        if (cart.Items.Count == 0)
        {
            throw new EmptyCartCheckoutException();
        }

        if (!restaurant.IsActive)
        {
            throw new RestaurantInactiveException();
        }

        if (!restaurant.IsOpenAt(TimeOnly.FromDateTime(currentTime)))
        {
            throw new RestaurantClosedException();
        }

        var unavailableItems = new List<UnavailableCheckoutItem>();
        var checkoutItems = new List<CheckoutItemSnapshot>();

        foreach (var cartItem in cart.Items)
        {
            var product = restaurant.FindProduct(cartItem.ProductId);

            if (product is null)
            {
                unavailableItems.Add(new UnavailableCheckoutItem(
                    cartItem.ProductId,
                    cartItem.ProductName,
                    "Product was not found in this restaurant."));
                continue;
            }

            if (!product.IsAvailable)
            {
                unavailableItems.Add(new UnavailableCheckoutItem(
                    product.Id,
                    product.Name,
                    "Product is currently unavailable."));
                continue;
            }

            checkoutItems.Add(new CheckoutItemSnapshot(
                product.Id,
                product.Name,
                product.CurrentPrice,
                cartItem.Quantity));
        }

        if (unavailableItems.Count > 0)
        {
            return new CheckoutProductsUnavailable(unavailableItems);
        }

        return new CheckoutSucceeded(checkoutItems);
    }
}
