using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Customers.CreateProfile;
using Talabat.Customer.API.Auth;
using Talabat.Application.Customers.GetProfile;
using Talabat.Application.Customers.UpdateProfile;
using Talabat.Customer.API.Contracts.Customer;
using Talabat.Customer.API.Extensions;
using Talabat.Customer.API.Middleware;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/profile")]
[Tags("Customer")]
[Produces("application/json")]
public sealed class CustomerController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly CreateCustomerProfileHandler _createProfileHandler;
    private readonly GetCustomerProfileHandler _getProfileHandler;
    private readonly UpdateCustomerProfileHandler _updateProfileHandler;

    public CustomerController(
        ICurrentUser currentUser,
        CreateCustomerProfileHandler createProfileHandler,
        GetCustomerProfileHandler getProfileHandler,
        UpdateCustomerProfileHandler updateProfileHandler)
    {
        _currentUser = currentUser;
        _createProfileHandler = createProfileHandler;
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
    }

    [HttpPost(Name = "CreateProfile")]
    [Authorize(Policy = AuthorizationPolicies.CustomerScopeOnly)]
    [Consumes("application/json")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerProfileCommand(
            _currentUser.UserId!.Value,
            request.FullName,
            request.Age,
            request.PhoneNumber);

        var result = await _createProfileHandler.Handle(command, cancellationToken);

        return result.ToCreatedAtAction(
            nameof(GetProfile),
            new { },
            id => Ok(new ProfileResponse(
                id,
                request.FullName,
                request.Age,
                request.PhoneNumber,
                [])));
    }

    [HttpGet(Name = "GetProfile")]
    [Authorize(Policy = AuthorizationPolicies.CustomerScopeOnly)]
    [RequireCustomerProfile(notFoundOnMissing: true)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await _getProfileHandler.Handle(
            new GetCustomerProfileQuery(_currentUser.CustomerId!.Value),
            cancellationToken);

        return result.ToActionResult(profile => Ok(MapToResponse(profile)));
    }

    [HttpPut(Name = "UpdateProfile")]
    [Authorize(Policy = AuthorizationPolicies.CustomerScopeOnly)]
    [RequireCustomerProfile]
    [Consumes("application/json")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerProfileCommand(
            _currentUser.CustomerId!.Value,
            request.FullName,
            request.Age,
            request.PhoneNumber);

        var result = await _updateProfileHandler.Handle(command, cancellationToken);

        return result.ToActionResult(profile => Ok(MapToResponse(profile)));
    }

    private static ProfileResponse MapToResponse(Application.Customers.Models.CustomerProfile profile)
    {
        var addresses = profile.Addresses.Select(a => new AddressDto(
            a.Id, a.Street, a.City, a.BuildingNumber, a.Floor, a.IsDefault)).ToList();

        return new ProfileResponse(
            profile.Id,
            profile.FullName,
            profile.Age,
            profile.PhoneNumber,
            addresses);
    }
}
