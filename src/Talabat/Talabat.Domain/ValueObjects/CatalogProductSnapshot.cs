using Talabat.Domain.Common;

namespace Talabat.Domain.ValueObjects;

public sealed record CatalogProductSnapshot
{
    public int ProductId { get; }

    public int RestaurantId { get; }

    public string ProductName { get; }

    public Money CurrentPrice { get; }

    public bool IsAvailable { get; }

    public CatalogProductSnapshot(
        int productId,
        int restaurantId,
        string productName,
        Money currentPrice,
        bool isAvailable)
    {
        ProductId = Guard.Positive(productId, nameof(productId));
        RestaurantId = Guard.Positive(restaurantId, nameof(restaurantId));
        ProductName = Guard.RequiredText(productName, nameof(productName));
        CurrentPrice = currentPrice ?? throw new ArgumentNullException(nameof(currentPrice));
        IsAvailable = isAvailable;
    }
}
