using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Basket.Models;

public sealed record CartDetails(
    int? Id,
    int CustomerId,
    int? RestaurantId,
    string Status,
    IReadOnlyCollection<CartLineItem> Items,
    Money CalculatedCurrentTotal,
    DateTime? ExpiresAtUtc)
{
    public static CartDetails Empty(int customerId)
    {
        return new CartDetails(
            Id: null,
            CustomerId: customerId,
            RestaurantId: null,
            Status: "Empty",
            Items: Array.Empty<CartLineItem>(),
            CalculatedCurrentTotal: Money.Zero,
            ExpiresAtUtc: null);
    }
}
