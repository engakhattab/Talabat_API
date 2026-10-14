using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Admin.API.Auth;
using Talabat.Admin.API.Contracts.Orders;
using Talabat.Application.Fulfillment.AcceptOrder;

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

    public OrdersController(AcceptOrderHandler acceptHandler)
    {
        _acceptHandler = acceptHandler ?? throw new ArgumentNullException(nameof(acceptHandler));
    }

    [HttpPost("{orderId:int}/accept", Name = "AcceptOrder")]
    [ProducesResponseType<AcceptOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Accept(int orderId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
