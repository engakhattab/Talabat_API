using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.GoOffline;
using Talabat.Application.DeliveryAgents.GoOnline;
using Talabat.Application.DeliveryAgents.GetCurrentStatus;
using Talabat.Delivery.API.Auth;
using Talabat.Delivery.API.Contracts;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/status")]
[Tags("AgentStatus")]
[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]
[Produces("application/json")]
public sealed class StatusController : ControllerBase
{
    private readonly GoOnlineHandler _goOnlineHandler;
    private readonly GoOfflineHandler _goOfflineHandler;
    private readonly GetCurrentDeliveryAgentStatusHandler _getCurrentStatusHandler;
    private readonly ICurrentUser _currentUser;

    public StatusController(
        GoOnlineHandler goOnlineHandler,
        GoOfflineHandler goOfflineHandler,
        GetCurrentDeliveryAgentStatusHandler getCurrentStatusHandler,
        ICurrentUser currentUser)
    {
        _goOnlineHandler = goOnlineHandler ?? throw new ArgumentNullException(nameof(goOnlineHandler));
        _goOfflineHandler = goOfflineHandler ?? throw new ArgumentNullException(nameof(goOfflineHandler));
        _getCurrentStatusHandler = getCurrentStatusHandler ?? throw new ArgumentNullException(nameof(getCurrentStatusHandler));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpGet(Name = "GetCurrentDeliveryAgentStatus")]
    [ProducesResponseType<DeliveryAgentStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetCurrentStatus(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int agentId)
            return Forbid();

        var result = await _getCurrentStatusHandler.Handle(
            new GetCurrentDeliveryAgentStatusQuery(agentId),
            cancellationToken);

        return result.ToActionResult(dto => Ok(new DeliveryAgentStatusResponse(dto.Status)));
    }

    [HttpPut("online", Name = "GoOnline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GoOnline(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _goOnlineHandler.Handle(
            new GoOnlineCommand(agentId),
            cancellationToken);

        return result.ToActionResult(_ => NoContent());
    }

    [HttpPut("offline", Name = "GoOffline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GoOffline(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _goOfflineHandler.Handle(
            new GoOfflineCommand(agentId),
            cancellationToken);

        return result.ToActionResult(_ => NoContent());
    }
}
