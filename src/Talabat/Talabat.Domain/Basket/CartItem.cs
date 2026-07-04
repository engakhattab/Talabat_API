using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Basket;

public sealed class CartItem
{
    public int ProductId { get; }

    public string ProductName { get; }

    public int Quantity { get; private set; }

    internal CartItem(int productId, string productName, int quantity)
    {
        ProductId = Guard.Positive(productId, nameof(productId));
        ProductName = Guard.RequiredText(productName, nameof(productName));
        EnsureValidQuantity(quantity);
        Quantity = quantity;
    }

    internal bool HasProduct(int productId)
    {
        return ProductId == productId;
    }

    internal void IncreaseQuantity(int quantity)
    {
        EnsureValidQuantity(quantity);
        Quantity = checked(Quantity + quantity);
    }

    internal void SetQuantity(int quantity)
    {
        EnsureValidQuantity(quantity);
        Quantity = quantity;
    }

    internal Money GetLineTotal(Money currentUnitPrice)
    {
        ArgumentNullException.ThrowIfNull(currentUnitPrice);

        return currentUnitPrice.Multiply(Quantity);
    }

    private static void EnsureValidQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidQuantityException();
        }
    }
}
