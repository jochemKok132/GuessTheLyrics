using System.Net;
using System.Security.Authentication;
using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace GuessTheLyrics.API.Handlers;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            AuthenticationException => (HttpStatusCode.Unauthorized, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),

            // The lyrics database is down, slow or unreachable.
            HttpRequestException => (HttpStatusCode.BadGateway, "The lyrics service is not reachable right now. Try again later."),
            TaskCanceledException when !httpContext.RequestAborted.IsCancellationRequested
                => (HttpStatusCode.GatewayTimeout, "The lyrics service took too long to answer. Try again later."),

            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (status >= HttpStatusCode.InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = (int)status;
        await httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message), cancellationToken);
        return true;
    }
}
