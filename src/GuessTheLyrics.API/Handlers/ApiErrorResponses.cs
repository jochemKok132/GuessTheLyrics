using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Handlers;

/// <summary>Error responses that don't come from an exception, in the same envelope as everything else.</summary>
public static class ApiErrorResponses
{
    /// <summary>
    /// Responses with an error status but no body (unknown route, wrong HTTP method, 415...) get an envelope too,
    /// so the UI never has to handle an empty or HTML error body from the API.
    /// </summary>
    public static IApplicationBuilder UseApiStatusCodePages(this IApplicationBuilder app) =>
        app.UseStatusCodePages(async context =>
        {
            var httpContext = context.HttpContext;
            if (!httpContext.Request.Path.StartsWithSegments("/api"))
                return;

            var error = ApiError.FromStatusCode(httpContext.Response.StatusCode);
            await httpContext.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(error.Message, error.ErrorCode, TraceIds.Get(httpContext)));
        });

    /// <summary>
    /// Replaces [ApiController]'s automatic ValidationProblemDetails (invalid JSON, wrong types, missing body)
    /// with an ApiResponse that has a readable message.
    /// </summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToList();

        // Keys starting with "$" come from the JSON reader: the body itself is broken.
        var message = errors.Any(e => e.Key.StartsWith('$') || e.Key == "request")
            ? "The request body is not valid JSON or has a value of the wrong type."
            : string.Join(" ", errors
                .SelectMany(e => e.Value!.Errors.Select(err =>
                    string.IsNullOrWhiteSpace(err.ErrorMessage) ? $"'{e.Key}' is not valid." : err.ErrorMessage))
                .Distinct()
                .Take(3));

        if (string.IsNullOrWhiteSpace(message))
            message = "The request is not valid.";

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail(message, ErrorCodes.Validation, TraceIds.Get(context.HttpContext)));
    }
}
