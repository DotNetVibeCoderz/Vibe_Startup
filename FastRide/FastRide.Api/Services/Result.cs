using FastRide.Shared.Common;

namespace FastRide.Api.Services;

/// <summary>Why an operation failed, mapped to an HTTP status at the edge.</summary>
public enum ResultStatus
{
    Success,
    NotFound,
    Conflict,
    Forbidden,
    Invalid
}

/// <summary>
/// Lets services report domain failures without knowing about HTTP, and keeps the endpoints
/// free of repeated "if null return NotFound" ladders.
/// </summary>
public readonly record struct Result<T>(ResultStatus Status, T? Value, string? Error)
{
    public bool IsSuccess => Status == ResultStatus.Success;

    public static Result<T> Ok(T value) => new(ResultStatus.Success, value, null);
    public static Result<T> NotFound(string error) => new(ResultStatus.NotFound, default, error);
    public static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
    public static Result<T> Forbidden(string error) => new(ResultStatus.Forbidden, default, error);
    public static Result<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);

    public IResult ToHttpResult() => Status switch
    {
        ResultStatus.Success => Results.Ok(Value),
        ResultStatus.NotFound => Results.NotFound(new ApiError("NotFound", Error)),
        ResultStatus.Conflict => Results.Conflict(new ApiError("Conflict", Error)),
        ResultStatus.Forbidden => Results.Json(new ApiError("Forbidden", Error), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(new ApiError("Invalid", Error))
    };

    /// <summary>Same as <see cref="ToHttpResult"/> but returns 201 with a Location header on success.</summary>
    public IResult ToCreatedResult(string location) => Status == ResultStatus.Success
        ? Results.Created(location, Value)
        : ToHttpResult();
}
