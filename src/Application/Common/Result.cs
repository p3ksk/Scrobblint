namespace Scrobblint.Application.Common;

/// <summary>The category of a failed <see cref="Result"/>, mapped to HTTP status codes at the edges.</summary>
public enum ResultError
{
    None = 0,
    Validation = 1,   // 400
    Unauthorized = 2, // 401
    Forbidden = 3,    // 403
    NotFound = 4,     // 404
    Conflict = 5      // 409
}

/// <summary>
/// Outcome of a service operation. Avoids using exceptions for expected failures
/// (validation, not-found, conflicts) so callers can map cleanly to HTTP / UI.
/// </summary>
public class Result
{
    protected Result(bool succeeded, ResultError error, string? message,
        IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        Succeeded = succeeded;
        Error = error;
        Message = message;
        ValidationErrors = validationErrors;
    }

    public bool Succeeded { get; }
    public bool Failed => !Succeeded;
    public ResultError Error { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public static Result Ok() => new(true, ResultError.None, null, null);

    public static Result Fail(ResultError error, string message) =>
        new(false, error, message, null);

    public static Result Invalid(IReadOnlyDictionary<string, string[]> errors, string message = "Validation failed.") =>
        new(false, ResultError.Validation, message, errors);

    public static Result NotFound(string message = "Not found.") => Fail(ResultError.NotFound, message);
    public static Result Conflict(string message) => Fail(ResultError.Conflict, message);
    public static Result Unauthorized(string message = "Unauthorized.") => Fail(ResultError.Unauthorized, message);
    public static Result Forbidden(string message = "Forbidden.") => Fail(ResultError.Forbidden, message);
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    private Result(bool succeeded, T? value, ResultError error, string? message,
        IReadOnlyDictionary<string, string[]>? validationErrors)
        : base(succeeded, error, message, validationErrors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(true, value, ResultError.None, null, null);

    public static new Result<T> Fail(ResultError error, string message) =>
        new(false, default, error, message, null);

    public static new Result<T> Invalid(IReadOnlyDictionary<string, string[]> errors, string message = "Validation failed.") =>
        new(false, default, ResultError.Validation, message, errors);

    public static new Result<T> NotFound(string message = "Not found.") => Fail(ResultError.NotFound, message);
    public static new Result<T> Conflict(string message) => Fail(ResultError.Conflict, message);
    public static new Result<T> Unauthorized(string message = "Unauthorized.") => Fail(ResultError.Unauthorized, message);
    public static new Result<T> Forbidden(string message = "Forbidden.") => Fail(ResultError.Forbidden, message);
}
