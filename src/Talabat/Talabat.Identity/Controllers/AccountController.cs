using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Identity.Controllers;

[ApiController]
[Route("account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IUserCapabilityService _capabilityService;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IUserCapabilityService capabilityService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _capabilityService = capabilityService;
    }

    [HttpPost("register/customer")]
    public async Task<IActionResult> RegisterCustomer(
        [FromBody] RegisterCustomerRequest req,
        CancellationToken ct = default)
    {
        var result = await _capabilityService.RegisterCustomerAsync(
            req.Email, req.Password, req.FullName, req.Age, req.PhoneNumber, ct);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        return Ok(new { id = result.Value, email = req.Email });
    }

    [HttpPost("register/delivery-agent")]
    public async Task<IActionResult> RegisterDeliveryAgent(
        [FromBody] RegisterDeliveryAgentRequest req,
        CancellationToken ct = default)
    {
        var result = await _capabilityService.RegisterDeliveryAgentApplicantAsync(
            req.Email, req.Password, req.FullName, req.VehicleType, req.PhoneNumber, ct);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        return Ok(new { id = result.Value, email = req.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _signInManager.PasswordSignInAsync(
            req.Email, req.Password, isPersistent: false, lockoutOnFailure: false);

        return result.Succeeded
            ? Ok(new { message = "logged in" })
            : Unauthorized();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "logged out" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        return user is null
            ? Unauthorized()
            : Ok(new { user.Id, user.Email });
    }

    private IActionResult MapError(ApplicationError error)
    {
        return error.Category switch
        {
            ApplicationErrorCategory.Validation => BadRequest(new { errors = new[] { error.Message } }),
            ApplicationErrorCategory.Conflict => Conflict(new { errors = new[] { error.Message } }),
            ApplicationErrorCategory.NotFound => NotFound(new { errors = new[] { error.Message } }),
            _ => BadRequest(new { errors = new[] { error.Message } })
        };
    }
}

public record RegisterCustomerRequest(
    string Email,
    string Password,
    string FullName,
    int Age,
    string? PhoneNumber);

public record RegisterDeliveryAgentRequest(
    string Email,
    string Password,
    string FullName,
    VehicleType VehicleType,
    string? PhoneNumber);

public record LoginRequest(string Email, string Password);
