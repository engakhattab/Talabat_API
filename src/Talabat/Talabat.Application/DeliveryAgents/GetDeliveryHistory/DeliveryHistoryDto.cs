using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.DeliveryAgents.GetDeliveryHistory;

public sealed record DeliveryHistoryDto(
    int Id,
    int OrderId,
    int CustomerId,
    int RestaurantId,
    DeliveryStatus Status,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    DateTime? AssignedAt,
    DateTime? DeliveredAt);
