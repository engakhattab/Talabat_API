using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.UpdateLocation;
using Talabat.Delivery.API.Auth;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/location")]
[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]
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

    [HttpPut]
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
