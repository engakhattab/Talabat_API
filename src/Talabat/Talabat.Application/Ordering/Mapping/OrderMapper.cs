using Talabat.Application.Ordering.Models;
using Talabat.Application.Ordering.Status;
using Talabat.Domain.Aggregates.Ordering;

namespace Talabat.Application.Ordering.Mapping;

public static class OrderMapper
{
    public static OrderSummary ToSummary(Order order, CustomerOrderProgress progress)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(progress);

        return new OrderSummary(
            order.Id,
            order.RestaurantId,
            order.CreatedAt,
            order.TotalAmount,
            order.Items.Sum(item => item.Quantity),
            progress.Status,
            progress.DeliveryAgentAssigned);
    }

    public static OrderDetails ToDetails(Order order, CustomerOrderProgress progress)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(progress);

        var address = new OrderDeliveryAddress(
            order.DeliveryAddress.Street,
            order.DeliveryAddress.City,
            order.DeliveryAddress.BuildingNumber,
            order.DeliveryAddress.Floor);

        var items = order.Items
            .Select(item => new OrderLineItem(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.LineTotal))
            .ToList()
            .AsReadOnly();

        return new OrderDetails(
            order.Id,
            order.CustomerId,
            order.RestaurantId,
            order.CreatedAt,
            address,
            items,
            order.TotalAmount,
            progress.Status,
            progress.DeliveryAgentAssigned);
    }
}
