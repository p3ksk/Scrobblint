using Scrobblint.Application.Common;
using Scrobblint.Shared.Common;

namespace Scrobblint.Api.Common;

/// <summary>
/// Translates the application <see cref="Result"/> type into HTTP responses, using the
/// <see cref="ApiError"/> envelope for failures.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.Succeeded
            ? Results.Json(result.Value, statusCode: successStatusCode)
            : Problem(result);

    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.Succeeded
            ? Results.StatusCode(successStatusCode)
            : Problem(result);

    private static IResult Problem(Result result)
    {
        var status = result.Error switch
        {
            ResultError.Validation => StatusCodes.Status400BadRequest,
            ResultError.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultError.Forbidden => StatusCodes.Status403Forbidden,
            ResultError.NotFound => StatusCodes.Status404NotFound,
            ResultError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        var error = new ApiError(result.Message ?? "Request failed.", result.ValidationErrors);
        return Results.Json(error, statusCode: status);
    }
}
