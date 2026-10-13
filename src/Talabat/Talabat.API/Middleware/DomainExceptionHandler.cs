using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Customer.API.Extensions;

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

        // Map through the same domain-exception contract the handlers use, so an exception that
        // escapes a handler (e.g. a RowVersion conflict raised by SaveChanges) keeps its category
        // (409, 404, 422, ...) instead of collapsing to 400.
        var problemDetails = DomainExceptionMapper.Map(exception).ToProblemDetails();

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
