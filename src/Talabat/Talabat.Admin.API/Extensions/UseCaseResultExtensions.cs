using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Common.Results;

namespace Talabat.Admin.API.Extensions;

public static class UseCaseResultExtensions
{
    public static IActionResult ToActionResult<T>(this UseCaseResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        var error = result.Error!;
        var statusCode = error.Category switch
        {
            ApplicationErrorCategory.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorCategory.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorCategory.Unavailable => StatusCodes.Status422UnprocessableEntity,
            ApplicationErrorCategory.OwnershipMismatch => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status404NotFound ? "Not Found" :
                statusCode == StatusCodes.Status409Conflict ? "Conflict" : "Bad Request",
            Detail = error.Message,
            Type = $"https://tools.ietf.org/html/rfc9110#section-{(statusCode == 404 ? "15.5.5" : statusCode == 409 ? "15.5.10" : "15.5.1")}" 
        };
        details.Extensions["errorCode"] = error.Code;
        return new ObjectResult(details) { StatusCode = statusCode };
    }
}
