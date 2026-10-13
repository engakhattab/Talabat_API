using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Deliveries;

/// <summary>
/// Builds the <see cref="Delivery"/> for a placed order, capturing an immutable snapshot of the
/// restaurant's name and pickup address so in-flight jobs are unaffected by later restaurant edits.
/// </summary>
internal static class DeliveryFactory
{
    public static Delivery ForOrder(
        int orderId,
        int customerId,
        Restaurant restaurant,
        DeliveryAddressSnapshot dropOffAddress,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(restaurant);

        var pickupAddress = new DeliveryAddressSnapshot(
            restaurant.PickupAddress.Street,
            restaurant.PickupAddress.City,
            restaurant.PickupAddress.BuildingNumber,
            restaurant.PickupAddress.Floor);

        return new Delivery(
            orderId,
            customerId,
            restaurant.Id,
            restaurant.Name,
            pickupAddress,
            dropOffAddress,
            createdAt);
    }
}
