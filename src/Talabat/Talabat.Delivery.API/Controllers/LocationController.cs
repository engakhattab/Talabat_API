using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.DeliveryAgents.UpdateLocation;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/location")]
[Authorize(Roles = "DeliveryAgent")]
public sealed class LocationController : ControllerBase
{
    private readonly UpdateLocationHandler _updateLocationHandler;

    public LocationController(UpdateLocationHandler updateLocationHandler)
    {
        _updateLocationHandler = updateLocationHandler ?? throw new ArgumentNullException(nameof(updateLocationHandler));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateLocationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _updateLocationHandler.Handle(command, cancellationToken);

        return result.ToActionResult(_ => Ok());
    }
}
