using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Delivery.API.Contracts.Deliveries;

public sealed record PendingDeliveryResponse(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string City,
    DateTime CreatedAt);
