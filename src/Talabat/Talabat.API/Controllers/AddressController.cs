using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Customers.AddAddress;
using Talabat.Application.Customers.RemoveAddress;
using Talabat.Application.Customers.SetDefaultAddress;
using Talabat.Customer.API.Contracts.Address;
using Talabat.Customer.API.Extensions;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/addresses")]
[Authorize]
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

    [HttpPost]
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

    [HttpDelete("{addressId:int}")]
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

    [HttpPut("{addressId:int}/default")]
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
            var addresses = profile.Addresses.Select(a => new Contracts.Customer.AddressDto(
                a.Id, a.Street, a.City, a.BuildingNumber, a.Floor, a.IsDefault)).ToList();

            return Ok(new Contracts.Customer.ProfileResponse(
                profile.Id,
                profile.FullName,
                profile.Age,
                profile.PhoneNumber,
                addresses));
        });
    }
}
