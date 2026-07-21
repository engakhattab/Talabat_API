using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed record PendingDeliveryDto(
    int Id,
    int OrderId,
    int CustomerId,
    int RestaurantId,
    DeliveryStatus Status,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    DateTime CreatedAt);
