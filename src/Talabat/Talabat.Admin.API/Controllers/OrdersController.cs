using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Admin.API.Auth;
using Talabat.Admin.API.Contracts.Orders;
using Talabat.Admin.API.Extensions;
using Talabat.Application.DeliveryAgents.ProgressRelease;
using Talabat.Application.Fulfillment;
using Talabat.Application.Fulfillment.AcceptOrder;
using Talabat.Application.Fulfillment.MarkReady;
using Talabat.Application.Fulfillment.StartPreparing;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Admin.API.Controllers;

/// <summary>Admin acts on behalf of a restaurant until the Restaurant Owner app exists (design D11).</summary>
[ApiController]
[Route("api/orders")]
[Tags("Orders")]
[Authorize(Policy = AuthorizationPolicies.AdminAccess)]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly AcceptOrderHandler _acceptHandler;
    private readonly StartPreparingHandler _startPreparingHandler;
    private readonly MarkReadyHandler _markReadyHandler;
    private readonly ReleaseDeliveryHandler _releaseDeliveryHandler;

    public OrdersController(
        AcceptOrderHandler acceptHandler,
        StartPreparingHandler startPreparingHandler,
        MarkReadyHandler markReadyHandler,
        ReleaseDeliveryHandler releaseDeliveryHandler)
    {
        _acceptHandler = acceptHandler ?? throw new ArgumentNullException(nameof(acceptHandler));
        _startPreparingHandler = startPreparingHandler ?? throw new ArgumentNullException(nameof(startPreparingHandler));
        _markReadyHandler = markReadyHandler ?? throw new ArgumentNullException(nameof(markReadyHandler));
        _releaseDeliveryHandler = releaseDeliveryHandler ?? throw new ArgumentNullException(nameof(releaseDeliveryHandler));
    }

    [HttpPost("{orderId:int}/accept", Name = "AcceptOrder")]
    [ProducesResponseType<AcceptOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(int orderId, CancellationToken cancellationToken)
    {
        var result = await _acceptHandler.Handle(
            new AcceptOrderCommand(orderId, new FulfillmentActor.Admin()),
            cancellationToken);

        return result.ToActionResult(accepted => Ok(new AcceptOrderResponse(
            accepted.OrderId,
            OrderFulfillmentStatus.Accepted.ToString(),
            accepted.DeliveryId,
            accepted.AcceptedAt)));
    }

    [HttpPost("{orderId:int}/start-preparing", Name = "StartPreparingOrder")]
    [ProducesResponseType<StartPreparingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartPreparing(int orderId, CancellationToken cancellationToken)
    {
        var result = await _startPreparingHandler.Handle(
            new StartPreparingCommand(orderId, new FulfillmentActor.Admin()),
            cancellationToken);

        return result.ToActionResult(preparing => Ok(new StartPreparingResponse(
            preparing.OrderId,
            OrderFulfillmentStatus.Preparing.ToString(),
            preparing.PreparingAt)));
    }

    [HttpPost("{orderId:int}/mark-ready", Name = "MarkOrderReady")]
    [ProducesResponseType<MarkReadyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkReady(int orderId, CancellationToken cancellationToken)
    {
        var result = await _markReadyHandler.Handle(
            new MarkReadyCommand(orderId, new FulfillmentActor.Admin()),
            cancellationToken);

        return result.ToActionResult(ready => Ok(new MarkReadyResponse(
            ready.OrderId,
            OrderFulfillmentStatus.ReadyForPickup.ToString(),
            ready.ReadyAt)));
    }

    /// <summary>
    /// Releases the order's delivery on behalf of an unresponsive agent (design D6, Q3): the delivery returns to the
    /// queue and the agent becomes Available. Allowed only before pickup.
    /// </summary>
    [HttpPost("{orderId:int}/release-agent", Name = "ReleaseOrderAgent")]
    [ProducesResponseType<ReleaseAgentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReleaseAgent(int orderId, CancellationToken cancellationToken)
    {
        var result = await _releaseDeliveryHandler.Handle(
            new ReleaseDeliveryCommand.ByAdmin(orderId),
            cancellationToken);

        return result.ToActionResult(released => Ok(new ReleaseAgentResponse(
            released.OrderId,
            released.DeliveryId,
            released.ReleasedAgentId,
            DeliveryStatus.PendingAssignment.ToString())));
    }
}
