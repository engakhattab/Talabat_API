using Talabat.Application.Basket.Models;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Basket.Mapping;

public static class CartMapper
{
    private static readonly TimeSpan CartExpirationPeriod = TimeSpan.FromHours(1);

    public static CartDetails ToDetails(Cart cart, Restaurant restaurant)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(restaurant);

        var currentPrices = restaurant.Products.ToDictionary(
            product => product.Id,
            product => product.CurrentPrice);

        var total = cart.GetTotal(currentPrices);

        var lineItems = cart.Items
            .Select(item =>
            {
                if (!currentPrices.TryGetValue(item.ProductId, out var currentPrice))
                {
                    throw new CurrentProductPriceMissingException(item.ProductId);
                }

                return new CartLineItem(
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    currentPrice,
                    currentPrice.Multiply(item.Quantity));
            })
            .ToList()
            .AsReadOnly();

        return new CartDetails(
            cart.Id,
            cart.CustomerId,
            cart.RestaurantId,
            cart.Status.ToString(),
            lineItems,
            total,
            cart.CreatedAt.Add(CartExpirationPeriod));
    }
}
