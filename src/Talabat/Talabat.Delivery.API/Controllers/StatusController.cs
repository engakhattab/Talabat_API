using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.DeliveryAgents.GoOffline;
using Talabat.Application.DeliveryAgents.GoOnline;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/status")]
[Authorize(Roles = "DeliveryAgent")]
public sealed class StatusController : ControllerBase
{
    private readonly GoOnlineHandler _goOnlineHandler;
    private readonly GoOfflineHandler _goOfflineHandler;

    public StatusController(
        GoOnlineHandler goOnlineHandler,
        GoOfflineHandler goOfflineHandler)
    {
        _goOnlineHandler = goOnlineHandler ?? throw new ArgumentNullException(nameof(goOnlineHandler));
        _goOfflineHandler = goOfflineHandler ?? throw new ArgumentNullException(nameof(goOfflineHandler));
    }

    [HttpPut("online")]
    public async Task<IActionResult> GoOnline(CancellationToken cancellationToken)
    {
        var result = await _goOnlineHandler.Handle(
            new GoOnlineCommand(),
            cancellationToken);

        return result.ToActionResult(_ => Ok());
    }

    [HttpPut("offline")]
    public async Task<IActionResult> GoOffline(CancellationToken cancellationToken)
    {
        var result = await _goOfflineHandler.Handle(
            new GoOfflineCommand(),
            cancellationToken);

        return result.ToActionResult(_ => Ok());
    }
}
