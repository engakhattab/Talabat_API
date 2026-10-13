using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Delivery.API.Contracts.Deliveries;

public sealed record DeliveryHistoryResponse(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    DateTime? AssignedAt,
    DateTime? DeliveredAt);
