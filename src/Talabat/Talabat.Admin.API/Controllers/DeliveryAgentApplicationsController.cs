using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Admin.API.Auth;
using Talabat.Admin.API.Contracts.DeliveryAgentApplications;
using Talabat.Admin.API.Extensions;
using Talabat.Application.Abstractions;
using Talabat.Application.DeliveryAgents.ApplicationReview;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Admin.API.Controllers;

[ApiController]
[Route("api/delivery-agent-applications")]
[Tags("DeliveryAgentApplications")]
[Authorize(Policy = AuthorizationPolicies.AdminAccess)]
[Produces("application/json")]
public sealed class DeliveryAgentApplicationsController : ControllerBase
{
    private readonly ListDeliveryAgentApplicationsHandler _listHandler;
    private readonly GetDeliveryAgentApplicationHandler _getHandler;
    private readonly IUserCapabilityService _capabilityService;

    public DeliveryAgentApplicationsController(
        ListDeliveryAgentApplicationsHandler listHandler,
        GetDeliveryAgentApplicationHandler getHandler,
        IUserCapabilityService capabilityService)
    {
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
        _getHandler = getHandler ?? throw new ArgumentNullException(nameof(getHandler));
        _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
    }

    [HttpGet(Name = "ListDeliveryAgentApplications")]
    [ProducesResponseType<IReadOnlyCollection<DeliveryAgentApplicationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var applicationStatus))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "status must be Pending, Approved, or Rejected.",
                Extensions = { ["errorCode"] = "InvalidApplicationStatus" }
            });
        }

        var applications = await _listHandler.Handle(applicationStatus, cancellationToken);
        return Ok(applications.Select(Map).ToArray());
    }

    [HttpGet("{userId:int}", Name = "GetDeliveryAgentApplication")]
    [ProducesResponseType<DeliveryAgentApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int userId, CancellationToken cancellationToken)
    {
        var result = await _getHandler.Handle(userId, cancellationToken);
        return result.ToActionResult(application => Ok(Map(application)));
    }

    [HttpPost("{userId:int}/approve", Name = "ApproveDeliveryAgentApplication")]
    [ProducesResponseType<ApplicationActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(int userId, CancellationToken cancellationToken)
    {
        var result = await _capabilityService.ApproveDeliveryAgentAsync(userId, cancellationToken);
        return result.ToActionResult(id => Ok(new ApplicationActionResponse(id, AgentApprovalStatus.Approved.ToString())));
    }

    [HttpPost("{userId:int}/reject", Name = "RejectDeliveryAgentApplication")]
    [ProducesResponseType<ApplicationActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(int userId, CancellationToken cancellationToken)
    {
        var result = await _capabilityService.RejectDeliveryAgentAsync(userId, cancellationToken);
        return result.ToActionResult(id => Ok(new ApplicationActionResponse(id, AgentApprovalStatus.Rejected.ToString())));
    }

    [HttpPost("{userId:int}/suspend", Name = "SuspendDeliveryAgent")]
    [ProducesResponseType<DeliveryAgentStatusActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(int userId, CancellationToken cancellationToken)
    {
        var result = await _capabilityService.SuspendDeliveryAgentAsync(userId, cancellationToken);
        return result.ToActionResult(id => Ok(new DeliveryAgentStatusActionResponse(id, DeliveryAgentStatus.Suspended.ToString())));
    }

    [HttpPost("{userId:int}/reactivate", Name = "ReactivateDeliveryAgent")]
    [ProducesResponseType<DeliveryAgentStatusActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(int userId, CancellationToken cancellationToken)
    {
        var result = await _capabilityService.ReactivateDeliveryAgentAsync(userId, cancellationToken);
        return result.ToActionResult(id => Ok(new DeliveryAgentStatusActionResponse(id, DeliveryAgentStatus.Offline.ToString())));
    }

    private static bool TryParseStatus(string? value, out AgentApprovalStatus status)
    {
        status = AgentApprovalStatus.PendingApproval;
        return value is null or "" or "Pending" or "PendingApproval"
            || Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);
    }

    private static DeliveryAgentApplicationResponse Map(DeliveryAgentApplicationReviewDto application) =>
        new(
            application.UserId,
            application.FullName,
            application.Email,
            application.PhoneNumber,
            application.VehicleType,
            application.ApplicationStatus,
            application.SubmittedAt);
}
