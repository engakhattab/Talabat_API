using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.ApplicantProfile;
using Talabat.Delivery.API.Auth;
using Talabat.Delivery.API.Contracts.ApplicantProfile;
using Talabat.Delivery.API.Extensions;

namespace Talabat.Delivery.API.Controllers;

[ApiController]
[Route("api/agent/me")]
[Tags("ApplicantProfile")]
[Authorize(Policy = AuthorizationPolicies.DeliveryApplicantAccess)]
[Produces("application/json")]
public sealed class ApplicantProfileController : ControllerBase
{
    private readonly GetDeliveryApplicantProfileHandler _getProfileHandler;
    private readonly UpdateDeliveryApplicantProfileHandler _updateProfileHandler;
    private readonly ICurrentUser _currentUser;

    public ApplicantProfileController(
        GetDeliveryApplicantProfileHandler getProfileHandler,
        UpdateDeliveryApplicantProfileHandler updateProfileHandler,
        ICurrentUser currentUser)
    {
        _getProfileHandler = getProfileHandler ?? throw new ArgumentNullException(nameof(getProfileHandler));
        _updateProfileHandler = updateProfileHandler ?? throw new ArgumentNullException(nameof(updateProfileHandler));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpGet("application", Name = "GetDeliveryApplicantApplication")]
    [ProducesResponseType<DeliveryApplicantApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplication(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId)
        {
            return Forbid();
        }

        var result = await _getProfileHandler.Handle(userId, cancellationToken);
        return result.ToActionResult(profile =>
            Ok(new DeliveryApplicantApplicationResponse(profile.ApplicationStatus)));
    }

    [HttpGet("profile", Name = "GetDeliveryApplicantProfile")]
    [ProducesResponseType<DeliveryApplicantProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId)
        {
            return Forbid();
        }

        var result = await _getProfileHandler.Handle(userId, cancellationToken);
        return result.ToActionResult(profile => Ok(Map(profile)));
    }

    [HttpPut("profile", Name = "UpdateDeliveryApplicantProfile")]
    [Consumes("application/json")]
    [ProducesResponseType<DeliveryApplicantProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateDeliveryApplicantProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int userId)
        {
            return Forbid();
        }

        var result = await _updateProfileHandler.Handle(
            userId,
            request.FullName,
            request.PhoneNumber,
            cancellationToken);
        return result.ToActionResult(profile => Ok(Map(profile)));
    }

    private static DeliveryApplicantProfileResponse Map(DeliveryApplicantProfileDto profile) =>
        new(
            profile.FullName,
            profile.PhoneNumber,
            profile.VehicleType,
            profile.ApplicationStatus,
            profile.DeliveryAgentStatus);
}
