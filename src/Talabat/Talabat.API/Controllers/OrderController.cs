using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Talabat.Application.Abstractions;
using Talabat.Customer.API.Auth;
using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.CancelOrder;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Application.Ordering.Status;
using Talabat.Customer.API.Contracts.Common;
using Talabat.Customer.API.Contracts.Orders;
using Talabat.Customer.API.Extensions;
using Talabat.Customer.API.Middleware;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/orders")]
[Tags("Orders")]
[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]
[RequireCustomerProfile]
[Produces("application/json")]
public sealed class OrderController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly GetOrderHistoryHandler _getOrderHistoryHandler;
    private readonly GetOrderDetailsHandler _getOrderDetailsHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;

    public OrderController(
        ICurrentUser currentUser,
        GetOrderHistoryHandler getOrderHistoryHandler,
        GetOrderDetailsHandler getOrderDetailsHandler,
        CancelOrderHandler cancelOrderHandler)
    {
        _currentUser = currentUser;
        _getOrderHistoryHandler = getOrderHistoryHandler;
        _getOrderDetailsHandler = getOrderDetailsHandler;
        _cancelOrderHandler = cancelOrderHandler;
    }

    [HttpGet(Name = "GetOrders")]
    [ProducesResponseType<OrderListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _getOrderHistoryHandler.Handle(
            new GetOrderHistoryQuery(_currentUser.CustomerId!.Value, page, pageSize),
            cancellationToken);

        return result.ToActionResult(orders =>
        {
            var items = orders.Items.Select(o => new OrderSummaryDto(
                o.Id,
                o.RestaurantId,
                new MoneyDto(o.TotalAmount.Amount),
                o.CreatedAtUtc,
                o.ItemCount,
                o.Status,
                o.DeliveryAgentAssigned)).ToList();

            return Ok(new OrderListResponse(
                items, orders.Page, orders.PageSize, orders.TotalCount));
        });
    }

    [HttpGet("{orderId:int}", Name = "GetOrderDetails")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
                order.CreatedAtUtc,
                order.Status,
                order.DeliveryAgentAssigned,
                order.CancelledBy,
                order.ReasonCode));
        });
    }

    /// <summary>
    /// Cancels the customer's own order while the restaurant has not accepted it (design D7). The body is optional:
    /// a reason is not required from the customer.
    /// </summary>
    [HttpPost("{orderId:int}/cancel", Name = "CancelOrder")]
    [ProducesResponseType<CancelOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrder(
        int orderId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _cancelOrderHandler.Handle(
            new CancelOrderCommand(_currentUser.CustomerId!.Value, orderId, request?.ReasonCode, request?.Note),
            cancellationToken);

        return result.ToActionResult(cancelled => Ok(new CancelOrderResponse(
            cancelled.OrderId,
            CustomerOrderStatus.Cancelled,
            cancelled.ReasonCode,
            cancelled.CancelledAt)));
    }
}
