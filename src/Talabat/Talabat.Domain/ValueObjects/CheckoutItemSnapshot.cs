using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;

namespace Talabat.Domain.ValueObjects;

public sealed record CheckoutItemSnapshot
{
    public int ProductId { get; }

    public string ProductName { get; }

    public Money UnitPrice { get; }

    public int Quantity { get; }

    public CheckoutItemSnapshot(
        int productId,
        string productName,
        Money unitPrice,
        int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidQuantityException();
        }

        ProductId = Guard.Positive(productId, nameof(productId));
        ProductName = Guard.RequiredText(productName, nameof(productName));
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
        Quantity = quantity;
    }
}
