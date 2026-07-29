using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Customer.API.Auth;
using Talabat.Application.Common.Results;
using Talabat.Application.Deliveries.CreateForOrder;
using Talabat.Application.Ordering.Checkout;
using Talabat.Customer.API.Contracts.Checkout;
using Talabat.Customer.API.Extensions;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/checkout")]
[Tags("Checkout")]
[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]
[Produces("application/json")]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly CheckoutHandler _checkoutHandler;
    private readonly CreateDeliveryForOrderHandler _createDeliveryHandler;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICurrentUser currentUser,
        CheckoutHandler checkoutHandler,
        CreateDeliveryForOrderHandler createDeliveryHandler,
        ILogger<CheckoutController> logger)
    {
        _currentUser = currentUser;
        _checkoutHandler = checkoutHandler;
        _createDeliveryHandler = createDeliveryHandler;
        _logger = logger;
    }

    [HttpPost(Name = "Checkout")]
    [Consumes("application/json")]
    [ProducesResponseType<CheckoutSuccessResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CheckoutUnavailableResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CheckoutCommand(
            _currentUser.CustomerId!.Value,
            request.DeliveryAddressId);

        var result = await _checkoutHandler.Handle(command, cancellationToken);

        if (result.IsSuccess && result.Value is CheckoutSucceededOutcome succeeded)
        {
            await TryCreateDeliveryAsync(succeeded, _currentUser.CustomerId!.Value, cancellationToken);
        }

        if (result.IsSuccess)
        {
            return result.ToActionResult(outcome =>
            {
                if (outcome is CheckoutSucceededOutcome ok)
                    return StatusCode(201, new CheckoutSuccessResponse(ok.OrderId));

                if (outcome is CheckoutProductsUnavailableOutcome unavailable)
                {
                    var items = unavailable.UnavailableItems
                        .Select(i => new UnavailableItemDto(i.ProductId, i.ProductName, i.Reason))
                        .ToList();

                    return StatusCode(422, new CheckoutUnavailableResponse(
                        "unavailable", items));
                }

                return StatusCode(500);
            });
        }

        return result.ToActionResult(_ => StatusCode(500));
    }

    private async Task TryCreateDeliveryAsync(
        CheckoutSucceededOutcome succeeded,
        int customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deliveryCmd = new CreateDeliveryForOrderCommand(
                succeeded.OrderId,
                customerId,
                succeeded.RestaurantId,
                succeeded.DeliveryAddress);

            await _createDeliveryHandler.Handle(deliveryCmd, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delivery creation failed for OrderId {OrderId}", succeeded.OrderId);
        }
    }
}
