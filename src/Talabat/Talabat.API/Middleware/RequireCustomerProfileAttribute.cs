using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Talabat.Application.Abstractions;

namespace Talabat.Customer.API.Middleware;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireCustomerProfileAttribute(bool notFoundOnMissing = false) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        if (!currentUser.IsAuthenticated || currentUser.HasCustomerCapability)
        {
            await next();
            return;
        }

        var status = notFoundOnMissing
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;

        var rfcSection = notFoundOnMissing ? "15.5.5" : "15.5.10";

        context.Result = new ObjectResult(new
        {
            type = $"https://tools.ietf.org/html/rfc9110#section-{rfcSection}",
            title = notFoundOnMissing ? "Not Found" : "Conflict",
            status,
            detail = "A customer profile has not been created yet. Use POST /api/me/profile to create one.",
            extensions = new { errorCode = "ProfileNotCreated" }
        }) { StatusCode = status };
    }
}
