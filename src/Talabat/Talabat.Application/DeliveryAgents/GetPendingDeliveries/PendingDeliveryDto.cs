using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed record PendingDeliveryDto(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string RestaurantName,
    string PickupStreet,
    string PickupCity,
    DateTime CreatedAt);
