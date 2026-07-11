using Talabat.Application.Ordering.Models;
using Talabat.Domain.Aggregates.Ordering;

namespace Talabat.Application.Ordering.Mapping;

public static class OrderMapper
{
    public static OrderSummary ToSummary(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderSummary(
            order.Id,
            order.RestaurantId,
            order.CreatedAt,
            order.TotalAmount);
    }

    public static OrderDetails ToDetails(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

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
            order.TotalAmount);
    }
}
