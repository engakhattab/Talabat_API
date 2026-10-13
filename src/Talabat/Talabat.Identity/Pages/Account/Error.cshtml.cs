using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Duende.IdentityServer.Models;

namespace Talabat.Identity.Pages.Account;

[AllowAnonymous]
public class ErrorModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;

    public ErrorModel(IIdentityServerInteractionService interaction)
    {
        _interaction = interaction;
    }

    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }

    public async Task OnGetAsync(string? errorId)
    {
        if (!string.IsNullOrEmpty(errorId))
        {
            var errorContext = await _interaction.GetErrorContextAsync(errorId, CancellationToken.None);
            Error = errorContext?.Error;
            ErrorDescription = errorContext?.ErrorDescription;
        }
    }
}
