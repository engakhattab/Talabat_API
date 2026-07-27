using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Deliveries.CreateForOrder;

public sealed record CreateDeliveryForOrderCommand(
    int OrderId,
    int CustomerId,
    int RestaurantId,
    DeliveryAddressSnapshot DeliveryAddress);
