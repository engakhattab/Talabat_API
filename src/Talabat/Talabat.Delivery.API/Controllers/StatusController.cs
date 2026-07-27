using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.GoOffline;
using Talabat.Application.DeliveryAgents.GoOnline;
using Talabat.Delivery.API.Auth;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/status")]
[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]
public sealed class StatusController : ControllerBase
{
    private readonly GoOnlineHandler _goOnlineHandler;
    private readonly GoOfflineHandler _goOfflineHandler;
    private readonly ICurrentUser _currentUser;

    public StatusController(
        GoOnlineHandler goOnlineHandler,
        GoOfflineHandler goOfflineHandler,
        ICurrentUser currentUser)
    {
        _goOnlineHandler = goOnlineHandler ?? throw new ArgumentNullException(nameof(goOnlineHandler));
        _goOfflineHandler = goOfflineHandler ?? throw new ArgumentNullException(nameof(goOfflineHandler));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpPut("online")]
    public async Task<IActionResult> GoOnline(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _goOnlineHandler.Handle(
            new GoOnlineCommand(agentId),
            cancellationToken);

        return result.ToActionResult(_ => Ok());
    }

    [HttpPut("offline")]
    public async Task<IActionResult> GoOffline(CancellationToken cancellationToken)
    {
        if (!TryGetAgentIdHelper.TryGetAgentId(_currentUser, out var agentId))
            return Forbid();

        var result = await _goOfflineHandler.Handle(
            new GoOfflineCommand(agentId),
            cancellationToken);

        return result.ToActionResult(_ => Ok());
    }
}
