using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed record PendingDeliveryDto(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string RestaurantName,
    string PickupStreet,
    string PickupCity,
    DateTime CreatedAt,
    OrderFulfillmentStatus FulfillmentStatus);
