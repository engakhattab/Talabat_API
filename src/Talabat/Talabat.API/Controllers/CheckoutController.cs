using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Checkout;
using Talabat.Customer.API.Contracts.Checkout;
using Talabat.Customer.API.Extensions;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/checkout")]
[Authorize]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly CheckoutHandler _checkoutHandler;

    public CheckoutController(
        ICurrentUser currentUser,
        CheckoutHandler checkoutHandler)
    {
        _currentUser = currentUser;
        _checkoutHandler = checkoutHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CheckoutCommand(
            _currentUser.CustomerId!.Value,
            request.DeliveryAddressId);

        var result = await _checkoutHandler.Handle(command, cancellationToken);

        if (result.IsSuccess)
        {
            return result.ToActionResult(outcome =>
            {
                if (outcome is CheckoutSucceededOutcome succeeded)
                {
                    return StatusCode(201, new CheckoutSuccessResponse(succeeded.OrderId));
                }

                if (outcome is CheckoutProductsUnavailableOutcome unavailable)
                {
                    var items = unavailable.UnavailableItems
                        .Select(i => new UnavailableItemDto(i.ProductId, i.ProductName, i.Reason))
                        .ToList();

                    var details = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
                        Title = "Checkout unavailable",
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Detail = "One or more items are unavailable for checkout."
                    };
                    details.Extensions["errorCode"] = "unavailable";
                    details.Extensions["unavailableItems"] = items;

                    return UnprocessableEntity(details);
                }

                return StatusCode(500);
            });
        }

        return result.ToActionResult(_ => StatusCode(500));
    }
}
