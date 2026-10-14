using Talabat.Application.Ordering.Status;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Ordering.Models;

public sealed record OrderDetails(
    int Id,
    int CustomerId,
    int RestaurantId,
    DateTime CreatedAtUtc,
    OrderDeliveryAddress DeliveryAddress,
    IReadOnlyCollection<OrderLineItem> Items,
    Money TotalAmount,
    CustomerOrderStatus Status,
    bool DeliveryAgentAssigned);
