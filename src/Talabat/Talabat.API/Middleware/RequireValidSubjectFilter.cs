using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Talabat.Application.Abstractions;

namespace Talabat.Customer.API.Middleware;

/// <summary>
/// Rejects authenticated principals whose subject claim is not a positive integer user id.
/// Such a principal cannot be mapped to a persisted user, so the request is unauthenticated
/// for business purposes and must not fall through to profile or capability checks.
/// </summary>
public sealed class RequireValidSubjectFilter : IAsyncActionFilter
{
    private readonly ICurrentUser _currentUser;

    public RequireValidSubjectFilter(ICurrentUser currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (_currentUser.IsAuthenticated && _currentUser.UserId is null)
        {
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The access token subject does not identify a valid user."
            };
            problem.Extensions["errorCode"] = "InvalidSubject";

            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        await next();
    }
}
