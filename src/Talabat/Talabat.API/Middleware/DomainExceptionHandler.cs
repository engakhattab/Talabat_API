using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Talabat.Domain.Exceptions;

namespace Talabat.Customer.API.Middleware;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (exception is not DomainException &&
            exception is not ArgumentException &&
            exception is not ArgumentOutOfRangeException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };

        problemDetails.Extensions["errorCode"] = exception.GetType().Name;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
