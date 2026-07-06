using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Domain.Aggregates.Ordering;

public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items;

    public int Id { get; }

    public int CustomerId { get; }

    public int RestaurantId { get; }

    public DeliveryAddressSnapshot DeliveryAddress { get; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money TotalAmount { get; }

    private Order(
        int id,
        int customerId,
        int restaurantId,
        List<OrderItem> items,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime createdAt,
        Money totalAmount)
    {
        Id = id;
        CustomerId = customerId;
        RestaurantId = restaurantId;
        _items = items;
        DeliveryAddress = deliveryAddress;
        CreatedAt = createdAt;
        TotalAmount = totalAmount;
    }

    public static Order CreateFromCheckout(
        int id,
        int customerId,
        int restaurantId,
        IEnumerable<CheckoutItemSnapshot> checkoutItems,
        DeliveryAddressSnapshot deliveryAddress,
        DateTime currentTime)
    {
        Guard.Positive(id, nameof(id));
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
            id,
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
