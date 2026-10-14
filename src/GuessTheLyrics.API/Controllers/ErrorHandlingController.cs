using GuessTheLyrics.API.Handlers;
using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GuessTheLyrics.API.Controllers;

/// <summary>
/// Base class for every API controller: runs an action and turns any exception into the standard error response.
/// <para>
/// Each action calls <see cref="HandleAsync{T}"/> / <see cref="Handle{T}"/> with the service call. On success the
/// result is wrapped in <see cref="ApiResponse{T}.Ok"/>; on failure <see cref="ApiError.FromException"/> picks the
/// status code, error code and message (e.g. LRCLIB down → 503 <c>lyrics_unavailable</c> with Retry-After), the
/// failure is logged with its trace id, and the player gets an <see cref="ApiResponse{T}.Fail"/>.
/// </para>
/// <para>
/// Why here and not only in middleware: the exception is caught in our own code, so it never "escapes" to the
/// framework. Expected failures like an unreachable lyrics service therefore don't make the Visual Studio debugger
/// stop as "user-unhandled", and the request always ends in a normal response. <see cref="GlobalExceptionHandler"/>
/// stays as the safety net for anything outside controllers (middleware, model binding, auth).
/// </para>
/// </summary>
[ApiController]
public abstract class ErrorHandlingController : ControllerBase
{
    protected async Task<ActionResult<ApiResponse<T>>> HandleAsync<T>(Func<Task<T>> action, string? successMessage = null)
    {
        try
        {
            return Ok(ApiResponse<T>.Ok(await action(), successMessage));
        }
        catch (Exception exception)
        {
            return Failure<T>(exception);
        }
    }

    protected ActionResult<ApiResponse<T>> Handle<T>(Func<T> action, string? successMessage = null)
    {
        try
        {
            return Ok(ApiResponse<T>.Ok(action(), successMessage));
        }
        catch (Exception exception)
        {
            return Failure<T>(exception);
        }
    }

    private ActionResult<ApiResponse<T>> Failure<T>(Exception exception)
    {
        var error = ApiError.FromException(exception, HttpContext.RequestAborted.IsCancellationRequested);
        var traceId = TraceIds.Get(HttpContext);

        var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
        ApiErrorLogging.Log(logger, error, exception, HttpContext, traceId);

        if (error.StatusCode == ApiError.ClientClosedRequest)
            return StatusCode(error.StatusCode); // the player left; nobody reads a body

        if (error.RetryAfter is { } retryAfter)
            Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        return StatusCode(error.StatusCode, ApiResponse<T>.Fail(error.Message, error.ErrorCode, traceId));
    }
}
