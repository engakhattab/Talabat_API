using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Ordering;

public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items;

    public int Id { get; private set; }

    public int CustomerId { get; private set; }

    public int RestaurantId { get; private set; }

    public DeliveryAddressSnapshot DeliveryAddress { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money TotalAmount { get; private set; }

    private Order()
    {
        _items = [];
        DeliveryAddress = new DeliveryAddressSnapshot(
            "Materialization",
            "Materialization",
            "0");
        TotalAmount = Money.Zero;
    }

    private Order(
        int customerId,
        int restaurantId,
        List<OrderItem> items,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime createdAt,
        Money totalAmount)
    {
        CustomerId = customerId;
        RestaurantId = restaurantId;
        _items = items;
        DeliveryAddress = deliveryAddress;
        CreatedAt = createdAt;
        TotalAmount = totalAmount;
    }

    public static Order CreateFromCheckout(
        int customerId,
        int restaurantId,
        IEnumerable<CheckoutItemSnapshot> checkoutItems,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime currentTime)
    {
        Guard.Positive(customerId, nameof(customerId));
        Guard.Positive(restaurantId, nameof(restaurantId));
        currentTime = Guard.Utc(currentTime, nameof(currentTime));
        ArgumentNullException.ThrowIfNull(checkoutItems);

        if (deliveryAddress is null)
        {
            throw new MissingDeliveryAddressException();
        }

        var snapshots = checkoutItems.ToList();

        if (snapshots.Count == 0)
        {
            throw new EmptyCartCheckoutException();
        }

        var items = new List<OrderItem>(snapshots.Count);
        var totalAmount = Money.Zero;

        foreach (var snapshot in snapshots)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var item = new OrderItem(
                snapshot.ProductId,
                snapshot.ProductName,
                snapshot.UnitPrice,
                snapshot.Quantity);

            items.Add(item);
            totalAmount = totalAmount.Add(item.LineTotal);
        }

        return new Order(
            customerId,
            restaurantId,
            items,
            deliveryAddress,
            currentTime,
            totalAmount);
    }

    public Money GetTotal()
    {
        return TotalAmount;
    }
}
