using Microsoft.AspNetCore.Diagnostics;
using Talabat.Admin.API.Extensions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;

namespace Talabat.Admin.API.Middleware;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (exception is not DomainException &&
            exception is not ArgumentException)
        {
            return false;
        }

        // Map through the same domain-exception contract the handlers use, so an exception that
        // escapes a handler (e.g. a RowVersion conflict raised by SaveChanges) keeps its category
        // (409, 404, 422, ...) instead of collapsing to 500 (design D12).
        var problemDetails = DomainExceptionMapper.Map(exception).ToProblemDetails();

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
