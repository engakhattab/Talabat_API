using Talabat.Domain.Common;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Ordering;

public sealed class OrderItem
{
    public int ProductId { get; }

    public string ProductName { get; }

    public Money UnitPrice { get; }

    public int Quantity { get; }

    public Money LineTotal { get; }

    private OrderItem()
    {
        ProductName = string.Empty;
        UnitPrice = Money.Zero;
        LineTotal = Money.Zero;
    }

    internal OrderItem(
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
        LineTotal = UnitPrice.Multiply(Quantity);
    }
}
