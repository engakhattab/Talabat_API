using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.DeliveryAgents.GetActiveDelivery;

public sealed record ActiveDeliveryDto(
    int Id,
    int OrderId,
    int RestaurantId,
    DeliveryStatus Status,
    string RestaurantName,
    string PickupStreet,
    string PickupCity,
    string PickupBuildingNumber,
    string? PickupFloor,
    string DropOffStreet,
    string DropOffCity,
    string DropOffBuildingNumber,
    string? DropOffFloor,
    string? CustomerPhoneNumber,
    DateTime? AssignedAt);
