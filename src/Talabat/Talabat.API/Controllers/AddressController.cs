using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Customer.API.Auth;
using Talabat.Application.Common.Results;
using Talabat.Application.Customers.AddAddress;
using Talabat.Application.Customers.RemoveAddress;
using Talabat.Application.Customers.SetDefaultAddress;
using Talabat.Customer.API.Contracts.Address;
using Talabat.Customer.API.Contracts.Customer;
using Talabat.Customer.API.Extensions;
using Talabat.Customer.API.Middleware;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/addresses")]
[Tags("Addresses")]
[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]
[RequireCustomerProfile]
[Produces("application/json")]
public sealed class AddressController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly AddCustomerAddressHandler _addAddressHandler;
    private readonly RemoveCustomerAddressHandler _removeAddressHandler;
    private readonly SetDefaultCustomerAddressHandler _setDefaultAddressHandler;

    public AddressController(
        ICurrentUser currentUser,
        AddCustomerAddressHandler addAddressHandler,
        RemoveCustomerAddressHandler removeAddressHandler,
        SetDefaultCustomerAddressHandler setDefaultAddressHandler)
    {
        _currentUser = currentUser;
        _addAddressHandler = addAddressHandler;
        _removeAddressHandler = removeAddressHandler;
        _setDefaultAddressHandler = setDefaultAddressHandler;
    }

    [HttpPost(Name = "AddAddress")]
    [Consumes("application/json")]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAddress(
        [FromBody] AddAddressRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddCustomerAddressCommand(
            _currentUser.CustomerId!.Value,
            request.Street,
            request.City,
            request.BuildingNumber,
            request.Floor,
            request.MakeDefault);

        var result = await _addAddressHandler.Handle(command, cancellationToken);

        return result.ToActionResult(profile =>
        {
            var lastAddress = profile.Addresses.LastOrDefault();
            if (lastAddress is null)
            {
                return StatusCode(201);
            }
            return Created($"/api/me/profile", new AddressResponse(
                lastAddress.Id,
                lastAddress.Street,
                lastAddress.City,
                lastAddress.BuildingNumber,
                lastAddress.Floor,
                lastAddress.IsDefault));
        });
    }

    [HttpDelete("{addressId:int}", Name = "RemoveAddress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAddress(
        int addressId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveCustomerAddressCommand(
            _currentUser.CustomerId!.Value,
            addressId);

        var result = await _removeAddressHandler.Handle(command, cancellationToken);

        return result.ToActionResult(_ => NoContent());
    }

    [HttpPut("{addressId:int}/default", Name = "SetDefaultAddress")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefaultAddress(
        int addressId,
        CancellationToken cancellationToken)
    {
        var command = new SetDefaultCustomerAddressCommand(
            _currentUser.CustomerId!.Value,
            addressId);

        var result = await _setDefaultAddressHandler.Handle(command, cancellationToken);

        return result.ToActionResult(profile =>
        {
            var addresses = profile.Addresses.Select(a => new AddressDto(
                a.Id, a.Street, a.City, a.BuildingNumber, a.Floor, a.IsDefault)).ToList();

            return Ok(new ProfileResponse(
                profile.Id,
                profile.FullName,
                profile.Age,
                profile.PhoneNumber,
                addresses));
        });
    }
}
