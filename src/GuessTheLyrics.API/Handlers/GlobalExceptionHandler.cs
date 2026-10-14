using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace GuessTheLyrics.API.Handlers;

/// <summary>
/// Central error handling: every exception that leaves a controller ends up here and becomes an
/// <see cref="ApiResponse{T}"/> with a status code, an <see cref="ErrorCodes">error code</see>, a message meant for
/// the player and the trace id from the log. Controllers and services never catch to build error responses themselves.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var error = ApiError.FromException(exception, httpContext.RequestAborted.IsCancellationRequested);
        var traceId = TraceIds.Get(httpContext);

        Log(error, exception, httpContext, traceId);

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

    private void Log(ApiError error, Exception exception, HttpContext httpContext, string traceId)
    {
        if (!logger.IsEnabled(error.LogLevel))
            return;

        // Expected failures (4xx) are logged without a stack trace to keep the log readable.
        var withStackTrace = error.LogLevel >= LogLevel.Warning ? exception : null;

        logger.Log(error.LogLevel, withStackTrace,
            "{Method} {Path} failed with {StatusCode} {ErrorCode} (trace {TraceId}): {ExceptionType}: {ExceptionMessage}",
            httpContext.Request.Method, httpContext.Request.Path, error.StatusCode, error.ErrorCode, traceId,
            exception.GetType().Name, exception.Message);
    }
}
