using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Talabat.Application.Abstractions;

namespace Talabat.Customer.API.Middleware;

public sealed class ProfileEnforcementFilter : IAsyncActionFilter
{
    private readonly ICurrentUser _currentUser;

    public ProfileEnforcementFilter(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
            path.Equals("/api/me/profile", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
            path.Equals("/api/me/profile", StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsAuthenticated &&
            !_currentUser.HasProfile)
        {
            context.Result = new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                title = "Not Found",
                status = 404,
                detail = "A customer profile has not been created yet. Use POST /api/me/profile to create one.",
                extensions = new { errorCode = "ProfileNotCreated" }
            });
            return;
        }

        if (path.StartsWith("/api/me/", StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsAuthenticated &&
            !_currentUser.HasProfile)
        {
            context.Result = new ConflictObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict",
                status = 409,
                detail = "A customer profile has not been created yet. Use POST /api/me/profile to create one.",
                extensions = new { errorCode = "ProfileNotCreated" }
            });
            return;
        }

        await next();
    }
}
