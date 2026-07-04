using Talabat.Domain.Common;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Catalog;

public sealed class Restaurant
{
    private readonly List<Product> _products = new();

    public int Id { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public string? ImageUrl { get; private set; }

    public TimeRange OpeningHours { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    public Restaurant(
        int id,
        string name,
        string description,
        string? imageUrl,
        TimeRange openingHours,
        bool isActive = true)
    {
        Id = Guard.Positive(id, nameof(id));
        Name = Guard.RequiredText(name, nameof(name));
        Description = Guard.RequiredText(description, nameof(description));
        ImageUrl = Guard.OptionalText(imageUrl);
        OpeningHours = openingHours ?? throw new ArgumentNullException(nameof(openingHours));
        IsActive = isActive;
    }

    public bool IsOpenAt(TimeOnly time)
    {
        return OpeningHours.Contains(time);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public Product AddProduct(
        int productId,
        string name,
        string description,
        Money currentPrice,
        string? imageUrl,
        bool isAvailable = true)
    {
        if (_products.Any(product => product.Id == productId))
        {
            throw new InvalidOperationException("Product already exists in this restaurant.");
        }

        var product = new Product(
            productId,
            Id,
            name,
            description,
            currentPrice,
            imageUrl,
            isAvailable);

        _products.Add(product);

        return product;
    }

    public Product? FindProduct(int productId)
    {
        return _products.SingleOrDefault(product => product.Id == productId);
    }

    public void UpdateProductPrice(int productId, Money currentPrice)
    {
        var product = GetRequiredProduct(productId);

        product.ChangeCurrentPrice(currentPrice);
    }

    public void MarkProductAvailable(int productId)
    {
        var product = GetRequiredProduct(productId);

        product.MarkAvailable();
    }

    public void MarkProductUnavailable(int productId)
    {
        var product = GetRequiredProduct(productId);

        product.MarkUnavailable();
    }

    private Product GetRequiredProduct(int productId)
    {
        return _products.SingleOrDefault(product => product.Id == productId)
            ?? throw new InvalidOperationException("Product was not found in this restaurant.");
    }
}