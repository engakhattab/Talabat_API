using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Customer.API.Contracts.Common;
using Talabat.Customer.API.Contracts.Orders;
using Talabat.Customer.API.Extensions;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/orders")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly GetOrderHistoryHandler _getOrderHistoryHandler;
    private readonly GetOrderDetailsHandler _getOrderDetailsHandler;

    public OrderController(
        ICurrentUser currentUser,
        GetOrderHistoryHandler getOrderHistoryHandler,
        GetOrderDetailsHandler getOrderDetailsHandler)
    {
        _currentUser = currentUser;
        _getOrderHistoryHandler = getOrderHistoryHandler;
        _getOrderDetailsHandler = getOrderDetailsHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _getOrderHistoryHandler.Handle(
            new GetOrderHistoryQuery(_currentUser.CustomerId!.Value),
            cancellationToken);

        return result.ToActionResult(orders =>
        {
            var items = orders.Select(o => new OrderSummaryDto(
                o.Id,
                o.RestaurantId,
                new MoneyDto(o.TotalAmount.Amount),
                o.CreatedAtUtc,
                0)).ToList();

            return Ok(new OrderListResponse(items, page, pageSize, items.Count));
        });
    }

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetOrderDetails(
        int orderId,
        CancellationToken cancellationToken)
    {
        var result = await _getOrderDetailsHandler.Handle(
            new GetOrderDetailsQuery(_currentUser.CustomerId!.Value, orderId),
            cancellationToken);

        return result.ToActionResult(order =>
        {
            var items = order.Items.Select(i => new OrderLineItemDto(
                i.ProductName,
                new MoneyDto(i.UnitPrice.Amount),
                i.Quantity,
                new MoneyDto(i.LineTotal.Amount))).ToList();

            var deliveryAddress = new OrderDeliveryAddressDto(
                order.DeliveryAddress.Street,
                order.DeliveryAddress.City,
                order.DeliveryAddress.BuildingNumber,
                order.DeliveryAddress.Floor);

            return Ok(new OrderDetailResponse(
                order.Id,
                order.CustomerId,
                order.RestaurantId,
                deliveryAddress,
                items,
                new MoneyDto(order.TotalAmount.Amount),
                order.CreatedAtUtc));
        });
    }
}
