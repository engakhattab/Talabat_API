using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly IUserCapabilityService _capabilityService;
    private readonly SignInManager<User> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;

    public RegisterModel(
        IUserCapabilityService capabilityService,
        SignInManager<User> signInManager,
        IIdentityServerInteractionService interaction)
    {
        _capabilityService = capabilityService;
        _signInManager = signInManager;
        _interaction = interaction;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public RegistrationVariant Variant { get; private set; } = RegistrationVariant.Customer;

    public async Task OnGetAsync()
    {
        Variant = await DeriveVariantAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Variant = await DeriveVariantAsync();

        if (Variant == RegistrationVariant.Customer && Input.Age is null)
        {
            ModelState.AddModelError(nameof(Input.Age), "Age is required for customer registration.");
        }

        if (Variant == RegistrationVariant.DeliveryAgent && Input.VehicleType is null)
        {
            ModelState.AddModelError(nameof(Input.VehicleType), "Vehicle type is required for delivery agent application.");
        }

        if (!ModelState.IsValid)
            return Page();

        UseCaseResult<int> result;

        if (Variant == RegistrationVariant.Customer)
        {
            result = await _capabilityService.RegisterCustomerAsync(
                Input.Email,
                Input.Password,
                Input.FullName,
                Input.Age!.Value,
                Input.PhoneNumber);
        }
        else
        {
            result = await _capabilityService.RegisterDeliveryAgentApplicantAsync(
                Input.Email,
                Input.Password,
                Input.FullName,
                Input.VehicleType!.Value,
                Input.PhoneNumber);
        }

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!.Message);
            return Page();
        }

        var signInResult = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, isPersistent: false, lockoutOnFailure: false);

        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Registration succeeded but sign-in failed. Please try logging in.");
            return Page();
        }

        var authContext = await _interaction.GetAuthorizationContextAsync(ReturnUrl, CancellationToken.None);
        if (authContext is not null)
            return Redirect(ReturnUrl!);
        if (Url.IsLocalUrl(ReturnUrl))
            return Redirect(ReturnUrl!);
        return Redirect("~/");
    }

    private async Task<RegistrationVariant> DeriveVariantAsync()
    {
        var authContext = await _interaction.GetAuthorizationContextAsync(ReturnUrl, CancellationToken.None);
        var clientId = authContext?.Client?.ClientId;

        return clientId switch
        {
            "talabat-customer-spa" => RegistrationVariant.Customer,
            "talabat-delivery-spa" => RegistrationVariant.DeliveryAgent,
            _ => RegistrationVariant.Customer
        };
    }

    public enum RegistrationVariant
    {
        Customer,
        DeliveryAgent
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public int? Age { get; set; }

        public VehicleType? VehicleType { get; set; }
    }
}
