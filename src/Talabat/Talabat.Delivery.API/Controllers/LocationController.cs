using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.UpdateLocation;
using Talabat.Delivery.API.Auth;
using Talabat.Delivery.API.Contracts;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/location")]
[Tags("AgentLocation")]
[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]
[Produces("application/json")]
public sealed class LocationController : ControllerBase
{
    private readonly UpdateLocationHandler _updateLocationHandler;
    private readonly ICurrentUser _currentUser;

    public LocationController(
        UpdateLocationHandler updateLocationHandler,
        ICurrentUser currentUser)
    {
        _updateLocationHandler = updateLocationHandler ?? throw new ArgumentNullException(nameof(updateLocationHandler));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpPut(Name = "UpdateLocation")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _updateLocationHandler.Handle(
            new UpdateLocationCommand(agentId, request.Latitude, request.Longitude),
            cancellationToken);

        return result.ToActionResult(_ => Ok());
    }
}
