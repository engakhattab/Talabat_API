using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Catalog;

public sealed class Product : AuditableEntity
{
    public int Id { get; private set; }

    public int RestaurantId { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public Money CurrentPrice { get; private set; }

    public bool IsAvailable { get; private set; }

    public string? ImageUrl { get; private set; }

    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        CurrentPrice = Money.Zero;
    }

    internal Product(
        int restaurantId,
        string name,
        string description,
        Money currentPrice,
        string? imageUrl,
        bool isAvailable = true)
    {
        RestaurantId = Guard.Positive(restaurantId, nameof(restaurantId));
        Name = Guard.RequiredText(name, nameof(name));
        Description = Guard.RequiredText(description, nameof(description));
        CurrentPrice = currentPrice ?? throw new ArgumentNullException(nameof(currentPrice));
        ImageUrl = Guard.OptionalText(imageUrl);
        IsAvailable = isAvailable;
    }

    internal void ChangeCurrentPrice(Money currentPrice)
    {
        CurrentPrice = currentPrice ?? throw new ArgumentNullException(nameof(currentPrice));
    }

    internal void MarkAvailable()
    {
        IsAvailable = true;
    }

    internal void MarkUnavailable()
    {
        IsAvailable = false;
    }
}
