using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SignInManager<User> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;

    public LogoutModel(SignInManager<User> signInManager, IIdentityServerInteractionService interaction)
    {
        _signInManager = signInManager;
        _interaction = interaction;
    }

    public string? PostLogoutRedirectUri { get; set; }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        await _signInManager.SignOutAsync();

        var ctx = await _interaction.GetLogoutContextAsync(logoutId, CancellationToken.None);
        if (!string.IsNullOrEmpty(ctx?.PostLogoutRedirectUri))
            return Redirect(ctx.PostLogoutRedirectUri);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? logoutId)
    {
        await _signInManager.SignOutAsync();

        var ctx = await _interaction.GetLogoutContextAsync(logoutId, CancellationToken.None);
        if (!string.IsNullOrEmpty(ctx?.PostLogoutRedirectUri))
            return Redirect(ctx.PostLogoutRedirectUri);

        return Page();
    }
}
