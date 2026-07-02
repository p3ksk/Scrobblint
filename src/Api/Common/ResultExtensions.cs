using Scrobblint.Application.Common;
using Scrobblint.Shared.Common;

namespace Scrobblint.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK, string? cacheControl = null) =>
        result.Succeeded
            ? JsonWithCache(result.Value!, successStatusCode, cacheControl)
            : Problem(result);

    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.Succeeded
            ? Results.StatusCode(successStatusCode)
            : Problem(result);

    private static IResult JsonWithCache<T>(T value, int statusCode, string? cacheControl)
    {
        if (cacheControl is null)
            return Results.Json(value, statusCode: statusCode);

        return new CacheResult(Results.Json(value, statusCode: statusCode), cacheControl);
    }

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

    private sealed class CacheResult : IResult
    {
        private readonly IResult _inner;
        private readonly string _cacheControl;

        public CacheResult(IResult inner, string cacheControl)
        {
            _inner = inner;
            _cacheControl = cacheControl;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = _cacheControl;
            await _inner.ExecuteAsync(httpContext);
        }
    }
}
