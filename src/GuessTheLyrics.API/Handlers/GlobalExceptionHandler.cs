using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace GuessTheLyrics.API.Handlers;

/// <summary>
/// Safety net for errors outside controllers (middleware, auth, model binding): controllers already handle their own
/// exceptions in <see cref="Controllers.ErrorHandlingController"/>. Anything that still escapes ends up here and becomes an
/// <see cref="ApiResponse{T}"/> with a status code, an <see cref="ErrorCodes">error code</see>, a message meant for
/// the player and the trace id from the log.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var error = ApiError.FromException(exception, httpContext.RequestAborted.IsCancellationRequested);
        var traceId = TraceIds.Get(httpContext);

        ApiErrorLogging.Log(logger, error, exception, httpContext, traceId);

        if (httpContext.Response.HasStarted)
        {
            // Part of the body is already sent; the status can't change any more. The log above is all we can do.
            return false;
        }

        httpContext.Response.StatusCode = error.StatusCode;

        if (error.StatusCode == ApiError.ClientClosedRequest)
            return true; // nobody is listening

        if (error.RetryAfter is { } retryAfter)
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(error.Message, error.ErrorCode, traceId),
            cancellationToken);

        return true;
    }
}
