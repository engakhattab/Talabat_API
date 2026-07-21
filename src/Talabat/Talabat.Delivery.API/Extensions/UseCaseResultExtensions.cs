using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Common.Results;

namespace Talabat.Delivery.API.Extensions;

public static class UseCaseResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this UseCaseResult<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        return MapError(result.Error!);
    }

    private static IActionResult MapError(ApplicationError error)
    {
        var statusCode = error.Category switch
        {
            ApplicationErrorCategory.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorCategory.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorCategory.Unavailable => StatusCodes.Status422UnprocessableEntity,
            ApplicationErrorCategory.OwnershipMismatch => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = error.Message,
            Type = $"https://tools.ietf.org/html/rfc9110#section-{GetRfcSection(statusCode)}"
        };

        problemDetails.Extensions["errorCode"] = error.Code;

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        _ => "Internal Server Error"
    };

    private static string GetRfcSection(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "15.5.1",
        StatusCodes.Status403Forbidden => "15.5.4",
        StatusCodes.Status404NotFound => "15.5.5",
        StatusCodes.Status409Conflict => "15.5.10",
        StatusCodes.Status422UnprocessableEntity => "15.5.21",
        _ => "15.6.1"
    };
}
