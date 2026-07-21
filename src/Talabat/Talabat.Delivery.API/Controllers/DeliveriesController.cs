using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.DeliveryAgents.AssignDelivery;
using Talabat.Application.DeliveryAgents.GetActiveDelivery;
using Talabat.Application.DeliveryAgents.GetDeliveryHistory;
using Talabat.Application.DeliveryAgents.GetPendingDeliveries;
using Talabat.Application.DeliveryAgents.ProgressArrive;
using Talabat.Application.DeliveryAgents.ProgressCancel;
using Talabat.Application.DeliveryAgents.ProgressDeliver;
using Talabat.Application.DeliveryAgents.ProgressFail;
using Talabat.Application.DeliveryAgents.ProgressOutForDelivery;
using Talabat.Application.DeliveryAgents.ProgressPickup;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/deliveries")]
[Authorize(Roles = "DeliveryAgent")]
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
        GetDeliveryHistoryHandler getDeliveryHistoryHandler)
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
    }

    // ── Query endpoints ────────────────────────────────────────────

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveDelivery(CancellationToken cancellationToken)
    {
        var result = await _getActiveDeliveryHandler.Handle(
            new GetActiveDeliveryQuery(),
            cancellationToken);

        return result.ToActionResult(dto => Ok(dto));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingDeliveries(CancellationToken cancellationToken)
    {
        var result = await _getPendingDeliveriesHandler.Handle(
            new GetPendingDeliveriesQuery(),
            cancellationToken);

        return result.ToActionResult(dtos => Ok(dtos));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetDeliveryHistory(CancellationToken cancellationToken)
    {
        var result = await _getDeliveryHistoryHandler.Handle(
            new GetDeliveryHistoryQuery(),
            cancellationToken);

        return result.ToActionResult(dtos => Ok(dtos));
    }

    // ── Assignment endpoint ─────────────────────────────────────────

    [HttpPost("{deliveryId:int}/assign")]
    public async Task<IActionResult> AssignDelivery(
        int deliveryId,
        [FromBody] AssignDeliveryBody body,
        CancellationToken cancellationToken)
    {
        var result = await _assignDeliveryAgentHandler.Handle(
            new AssignDeliveryCommand(deliveryId, body.AgentId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    // ── Lifecycle endpoints ─────────────────────────────────────────

    [HttpPost("{deliveryId:int}/out-for-delivery")]
    public async Task<IActionResult> OutForDelivery(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var result = await _outForDeliveryHandler.Handle(
            new OutForDeliveryCommand(deliveryId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    [HttpPost("{deliveryId:int}/arrived-at-restaurant")]
    public async Task<IActionResult> ArrivedAtRestaurant(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var result = await _arrivedAtRestaurantHandler.Handle(
            new ArrivedAtRestaurantCommand(deliveryId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    [HttpPost("{deliveryId:int}/picked-up")]
    public async Task<IActionResult> PickUpOrder(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var result = await _pickUpOrderHandler.Handle(
            new PickUpOrderCommand(deliveryId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    [HttpPost("{deliveryId:int}/delivered")]
    public async Task<IActionResult> DeliverOrder(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var result = await _deliverOrderHandler.Handle(
            new DeliverOrderCommand(deliveryId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    [HttpPost("{deliveryId:int}/cancel")]
    public async Task<IActionResult> CancelDelivery(
        int deliveryId,
        CancellationToken cancellationToken)
    {
        var result = await _cancelDeliveryHandler.Handle(
            new CancelDeliveryCommand(deliveryId),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    [HttpPost("{deliveryId:int}/fail")]
    public async Task<IActionResult> FailDelivery(
        int deliveryId,
        [FromBody] FailDeliveryBody body,
        CancellationToken cancellationToken)
    {
        var result = await _failDeliveryHandler.Handle(
            new FailDeliveryCommand(deliveryId, body.Reason),
            cancellationToken);

        return result.ToActionResult(id => Ok(id));
    }

    // ── Request bodies ─────────────────────────────────────────────

    public sealed record AssignDeliveryBody(int AgentId);

    public sealed record FailDeliveryBody(string Reason);
}
