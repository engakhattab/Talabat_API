using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Basket;

public sealed class Cart
{
    private static readonly TimeSpan ExpirationPeriod = TimeSpan.FromHours(1);
    private readonly List<CartItem> _items = [];

    public int Id { get; }

    public int CustomerId { get; }

    public int RestaurantId { get; private set; }

    public DateTime CreatedAt { get; }

    public CartStatus Status { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public Cart(int id, int customerId, DateTime createdAt)
    {
        Id = Guard.Positive(id, nameof(id));
        CustomerId = Guard.Positive(customerId, nameof(customerId));
        CreatedAt = createdAt;
        Status = CartStatus.Active;
    }

    public bool IsExpired(DateTime currentTime)
    {
        return currentTime >= CreatedAt.Add(ExpirationPeriod);
    }

    public void AddItem(
        CatalogProductSnapshot productSnapshot,
        int quantity,
        DateTime currentTime)
    {
        EnsureCanBeModified(currentTime);
        ArgumentNullException.ThrowIfNull(productSnapshot);
        EnsureValidQuantity(quantity);

        if (!productSnapshot.IsAvailable)
        {
            throw new ProductUnavailableException();
        }

        if (_items.Count == 0)
        {
            RestaurantId = productSnapshot.RestaurantId;
        }
        else if (RestaurantId != productSnapshot.RestaurantId)
        {
            throw new CrossRestaurantCartException();
        }

        var existingItem = _items.SingleOrDefault(
            item => item.HasProduct(productSnapshot.ProductId));

        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
            return;
        }

        _items.Add(new CartItem(
            productSnapshot.ProductId,
            productSnapshot.ProductName,
            quantity));
    }

    public void UpdateQuantity(int productId, int quantity, DateTime currentTime)
    {
        EnsureCanBeModified(currentTime);
        GetRequiredItem(productId).SetQuantity(quantity);
    }

    public void RemoveItem(int productId, DateTime currentTime)
    {
        EnsureCanBeModified(currentTime);
        _items.Remove(GetRequiredItem(productId));
    }

    public void Clear(DateTime currentTime)
    {
        EnsureCanBeModified(currentTime);
        _items.Clear();
        Status = CartStatus.Cleared;
    }

    public void MarkCheckedOut(DateTime currentTime)
    {
        EnsureCanBeModified(currentTime);

        if (_items.Count == 0)
        {
            throw new EmptyCartCheckoutException();
        }

        Status = CartStatus.CheckedOut;
    }

    public Money GetTotal(IReadOnlyDictionary<int, Money> currentPrices)
    {
        ArgumentNullException.ThrowIfNull(currentPrices);

        var total = Money.Zero;

        foreach (var item in _items)
        {
            if (!currentPrices.TryGetValue(item.ProductId, out var currentPrice))
            {
                throw new InvalidOperationException(
                    $"Current price is missing for product {item.ProductId}.");
            }

            total = total.Add(item.GetLineTotal(currentPrice));
        }

        return total;
    }

    private void EnsureCanBeModified(DateTime currentTime)
    {
        if (Status != CartStatus.Active)
        {
            throw new CartNotActiveException();
        }

        if (IsExpired(currentTime))
        {
            throw new CartExpiredException();
        }
    }

    private CartItem GetRequiredItem(int productId)
    {
        productId = Guard.Positive(productId, nameof(productId));

        return _items.SingleOrDefault(item => item.HasProduct(productId))
            ?? throw new InvalidOperationException(
                $"Cart item for product {productId} was not found.");
    }

    private static void EnsureValidQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidQuantityException();
        }
    }
}
