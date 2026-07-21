using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.DeliveryAgents.GetActiveDelivery;

public sealed record ActiveDeliveryDto(
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
