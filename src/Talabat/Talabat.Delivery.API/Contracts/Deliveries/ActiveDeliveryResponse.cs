using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Delivery.API.Contracts.Deliveries;

public sealed record ActiveDeliveryResponse(
    int Id,
    int OrderId,
    int CustomerId,
    int RestaurantId,
    DeliveryStatus Status,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    DateTime? AssignedAt);
