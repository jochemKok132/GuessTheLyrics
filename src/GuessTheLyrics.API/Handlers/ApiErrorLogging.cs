namespace GuessTheLyrics.API.Handlers;

public static class ApiErrorLogging
{
    /// <summary>One log line per failed request, at the level <see cref="ApiError"/> decided.</summary>
    public static void Log(ILogger logger, ApiError error, Exception exception, HttpContext httpContext, string traceId)
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
