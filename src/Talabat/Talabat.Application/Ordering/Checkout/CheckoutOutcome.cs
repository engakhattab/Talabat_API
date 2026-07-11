using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Ordering.Checkout;

public abstract class CheckoutOutcome
{
    private protected CheckoutOutcome()
    {
    }
}

public sealed class CheckoutSucceededOutcome : CheckoutOutcome
{
    public int OrderId { get; }

    public Money TotalAmount { get; }

    public CheckoutSucceededOutcome(int orderId, Money totalAmount)
    {
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderId),
                orderId,
                "Order id must be greater than zero.");
        }

        OrderId = orderId;
        TotalAmount = totalAmount ?? throw new ArgumentNullException(nameof(totalAmount));
    }
}

public sealed class CheckoutProductsUnavailableOutcome : CheckoutOutcome
{
    public IReadOnlyCollection<UnavailableCheckoutItemOutcome> UnavailableItems { get; }

    public CheckoutProductsUnavailableOutcome(
        IEnumerable<UnavailableCheckoutItemOutcome> unavailableItems)
    {
        ArgumentNullException.ThrowIfNull(unavailableItems);

        var items = unavailableItems.ToList();

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "Unavailable checkout items cannot be empty.",
                nameof(unavailableItems));
        }

        if (items.Any(item => item is null))
        {
            throw new ArgumentException(
                "Unavailable checkout items cannot contain null values.",
                nameof(unavailableItems));
        }

        UnavailableItems = items.AsReadOnly();
    }
}

public sealed class UnavailableCheckoutItemOutcome
{
    public int ProductId { get; }

    public string ProductName { get; }

    public string Reason { get; }

    public UnavailableCheckoutItemOutcome(
        int productId,
        string productName,
        string reason)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Product id must be greater than zero.");
        }

        ProductId = productId;
        ProductName = NormalizeRequiredText(productName, nameof(productName));
        Reason = NormalizeRequiredText(reason, nameof(reason));
    }

    private static string NormalizeRequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} is required and cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }
}
