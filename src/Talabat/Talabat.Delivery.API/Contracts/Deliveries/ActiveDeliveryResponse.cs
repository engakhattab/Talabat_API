using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Delivery.API.Contracts.Deliveries;

public sealed record ActiveDeliveryResponse(
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
