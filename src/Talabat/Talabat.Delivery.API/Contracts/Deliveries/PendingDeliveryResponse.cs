using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Delivery.API.Contracts.Deliveries;

public sealed record PendingDeliveryResponse(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string RestaurantName,
    string PickupStreet,
    string PickupCity,
    DateTime CreatedAt,
    OrderFulfillmentStatus FulfillmentStatus);
