using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.AssignDelivery;
using Talabat.Delivery.API.Auth;
using Talabat.Application.DeliveryAgents.GetActiveDelivery;
using Talabat.Application.DeliveryAgents.GetDeliveryHistory;
using Talabat.Application.DeliveryAgents.GetPendingDeliveries;
using Talabat.Application.DeliveryAgents.ProgressArrive;
using Talabat.Application.DeliveryAgents.ProgressCancel;
using Talabat.Application.DeliveryAgents.ProgressDeliver;
using Talabat.Application.DeliveryAgents.ProgressFail;
using Talabat.Application.DeliveryAgents.ProgressOutForDelivery;
using Talabat.Application.DeliveryAgents.ProgressPickup;
using Talabat.Delivery.API.Contracts.Deliveries;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/deliveries")]
[Tags("Deliveries")]
[Authorize(Policy = AuthorizationPolicies.OperationalDeliveryAgentAccess)]
[Produces("application/json")]
public sealed class DeliveriesController : ControllerBase
{
    private readonly OutForDeliveryHandler _outForDeliveryHandler;
    private readonly ArrivedAtRestaurantHandler _arrivedAtRestaurantHandler;
    private readonly PickUpOrderHandler _pickUpOrderHandler;
    private readonly DeliverOrderHandler _deliverOrderHandler;
    private readonly CancelDeliveryHandler _cancelDeliveryHandler;
    private readonly FailDeliveryHandler _failDeliveryHandler;
    private readonly AssignDeliveryAgentHandler _assignDeliveryAgentHandler;
    private readonly GetActiveDeliveryHandler _getActiveDeliveryHandler;
    private readonly GetPendingDeliveriesHandler _getPendingDeliveriesHandler;
    private readonly GetDeliveryHistoryHandler _getDeliveryHistoryHandler;
    private readonly ICurrentUser _currentUser;

    public DeliveriesController(
        OutForDeliveryHandler outForDeliveryHandler,
        ArrivedAtRestaurantHandler arrivedAtRestaurantHandler,
        PickUpOrderHandler pickUpOrderHandler,
        DeliverOrderHandler deliverOrderHandler,
        CancelDeliveryHandler cancelDeliveryHandler,
        FailDeliveryHandler failDeliveryHandler,
        AssignDeliveryAgentHandler assignDeliveryAgentHandler,
        GetActiveDeliveryHandler getActiveDeliveryHandler,
        GetPendingDeliveriesHandler getPendingDeliveriesHandler,
        GetDeliveryHistoryHandler getDeliveryHistoryHandler,
        ICurrentUser currentUser)
    {
        _outForDeliveryHandler = outForDeliveryHandler ?? throw new ArgumentNullException(nameof(outForDeliveryHandler));
        _arrivedAtRestaurantHandler = arrivedAtRestaurantHandler ?? throw new ArgumentNullException(nameof(arrivedAtRestaurantHandler));
        _pickUpOrderHandler = pickUpOrderHandler ?? throw new ArgumentNullException(nameof(pickUpOrderHandler));
        _deliverOrderHandler = deliverOrderHandler ?? throw new ArgumentNullException(nameof(deliverOrderHandler));
        _cancelDeliveryHandler = cancelDeliveryHandler ?? throw new ArgumentNullException(nameof(cancelDeliveryHandler));
        _failDeliveryHandler = failDeliveryHandler ?? throw new ArgumentNullException(nameof(failDeliveryHandler));
        _assignDeliveryAgentHandler = assignDeliveryAgentHandler ?? throw new ArgumentNullException(nameof(assignDeliveryAgentHandler));
        _getActiveDeliveryHandler = getActiveDeliveryHandler ?? throw new ArgumentNullException(nameof(getActiveDeliveryHandler));
        _getPendingDeliveriesHandler = getPendingDeliveriesHandler ?? throw new ArgumentNullException(nameof(getPendingDeliveriesHandler));
        _getDeliveryHistoryHandler = getDeliveryHistoryHandler ?? throw new ArgumentNullException(nameof(getDeliveryHistoryHandler));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    // ── Query endpoints ────────────────────────────────────────────

    [HttpGet("active", Name = "GetActiveDelivery")]
    [ProducesResponseType<ActiveDeliveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetActiveDelivery(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _getActiveDeliveryHandler.Handle(
            new GetActiveDeliveryQuery(agentId),
            cancellationToken);

        return result.ToActionResult(dto => Ok(MapToActiveDelivery(dto)));
    }

    [HttpGet("pending", Name = "GetPendingDeliveries")]
    [ProducesResponseType<IReadOnlyCollection<PendingDeliveryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingDeliveries(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _getPendingDeliveriesHandler.Handle(
            new GetPendingDeliveriesQuery(agentId),
            cancellationToken);

        return result.ToActionResult(dtos =>
            Ok(dtos.Select(MapToPendingDelivery).ToList()));
    }

    [HttpGet("history", Name = "GetDeliveryHistory")]
    [ProducesResponseType<IReadOnlyCollection<DeliveryHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDeliveryHistory(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _getDeliveryHistoryHandler.Handle(
            new GetDeliveryHistoryQuery(agentId),
            cancellationToken);

        return result.ToActionResult(dtos =>
            Ok(dtos.Select(MapToDeliveryHistory).ToList()));
    }

    // ── Assignment endpoint ─────────────────────────────────────────

    [HttpPost("{deliveryId:int}/assign", Name = "AssignDelivery")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignDelivery(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _assignDeliveryAgentHandler.Handle(
            new AssignDeliveryCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    // ── Lifecycle endpoints ─────────────────────────────────────────

    [HttpPost("{deliveryId:int}/out-for-delivery", Name = "OutForDelivery")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OutForDelivery(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _outForDeliveryHandler.Handle(
            new OutForDeliveryCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    [HttpPost("{deliveryId:int}/arrived-at-restaurant", Name = "ArrivedAtRestaurant")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArrivedAtRestaurant(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _arrivedAtRestaurantHandler.Handle(
            new ArrivedAtRestaurantCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    [HttpPost("{deliveryId:int}/picked-up", Name = "PickUpOrder")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PickUpOrder(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _pickUpOrderHandler.Handle(
            new PickUpOrderCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    [HttpPost("{deliveryId:int}/delivered", Name = "DeliverOrder")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeliverOrder(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _deliverOrderHandler.Handle(
            new DeliverOrderCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    [HttpPost("{deliveryId:int}/cancel", Name = "CancelDelivery")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelDelivery(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _cancelDeliveryHandler.Handle(
            new CancelDeliveryCommand(deliveryId, agentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    [HttpPost("{deliveryId:int}/fail", Name = "FailDelivery")]
    [Consumes("application/json")]
    [ProducesResponseType<DeliveryIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FailDelivery(
        int deliveryId,
        [FromBody] FailDeliveryBody body,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _failDeliveryHandler.Handle(
            new FailDeliveryCommand(deliveryId, agentId, body.Reason),
            cancellationToken);

        return result.ToActionResult(id => Ok(new DeliveryIdResponse(id)));
    }

    // ── Mapping ────────────────────────────────────────────────────

    private static ActiveDeliveryResponse MapToActiveDelivery(ActiveDeliveryDto dto) =>
        new(dto.Id, dto.OrderId, dto.RestaurantId, dto.Status, dto.RestaurantName,
            dto.PickupStreet, dto.PickupCity, dto.PickupBuildingNumber, dto.PickupFloor,
            dto.DropOffStreet, dto.DropOffCity, dto.DropOffBuildingNumber, dto.DropOffFloor,
            dto.CustomerPhoneNumber, dto.AssignedAt);

    private static PendingDeliveryResponse MapToPendingDelivery(PendingDeliveryDto dto) =>
        new(dto.Id, dto.OrderId, dto.RestaurantId, dto.Status, dto.RestaurantName,
            dto.PickupStreet, dto.PickupCity, dto.CreatedAt);

    private static DeliveryHistoryResponse MapToDeliveryHistory(DeliveryHistoryDto dto) =>
        new(dto.Id, dto.OrderId, dto.RestaurantId,
            dto.Status, dto.Street, dto.City, dto.BuildingNumber, dto.Floor,
            dto.AssignedAt, dto.DeliveredAt);

    // ── Request bodies ─────────────────────────────────────────────

    public sealed record FailDeliveryBody(string Reason);
}
