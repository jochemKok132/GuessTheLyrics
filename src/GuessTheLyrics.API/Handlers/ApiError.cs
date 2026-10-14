using System.Data.Common;
using System.Security.Authentication;
using System.Text.Json;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GuessTheLyrics.API.Handlers;

/// <summary>What the API answers for a failure: status, error code, message for the player, and how loud to log it.</summary>
public sealed record ApiError(int StatusCode, string ErrorCode, string Message, LogLevel LogLevel, TimeSpan? RetryAfter = null)
{
    /// <summary>Non-standard "client closed request" status (nginx convention); the response is never delivered.</summary>
    public const int ClientClosedRequest = 499;

    private const string TryAgain = "Try again in a moment.";

    /// <summary>
    /// The single place that decides how an exception is reported. Services throw meaningful exceptions
    /// (see the Domain/Exceptions folder and the conventions in the README); everything unknown becomes a 500.
    /// </summary>
    public static ApiError FromException(Exception exception, bool requestAborted) => exception switch
    {
        // The player closed the tab or navigated away: nothing to report.
        OperationCanceledException when requestAborted =>
            new(ClientClosedRequest, ErrorCodes.ClientClosedRequest, "Request cancelled.", LogLevel.Debug),

        // ----- Caller errors (4xx) -----

        // Only plain ArgumentException carries a message written for players (Validate() extensions);
        // ArgumentNullException / ArgumentOutOfRangeException are programming errors and end up as 500.
        ArgumentException when exception.GetType() == typeof(ArgumentException) =>
            new(StatusCodes.Status400BadRequest, ErrorCodes.Validation, exception.Message, LogLevel.Information),

        JsonException =>
            new(StatusCodes.Status400BadRequest, ErrorCodes.Validation, "The request body is not valid JSON.", LogLevel.Information),

        BadHttpRequestException bad => bad.StatusCode switch
        {
            StatusCodes.Status413PayloadTooLarge =>
                new(bad.StatusCode, ErrorCodes.PayloadTooLarge, "The request is too large.", LogLevel.Information),
            StatusCodes.Status408RequestTimeout =>
                new(bad.StatusCode, ErrorCodes.Timeout, "The request took too long to arrive.", LogLevel.Information),
            _ => new(StatusCodes.Status400BadRequest, ErrorCodes.Validation, "The request could not be read.", LogLevel.Information)
        },

        AuthenticationException =>
            new(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, exception.Message, LogLevel.Information),

        UnauthorizedAccessException =>
            new(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "You are not allowed to do this.", LogLevel.Warning),

        KeyNotFoundException =>
            new(StatusCodes.Status404NotFound, ErrorCodes.NotFound, exception.Message, LogLevel.Information),

        ConflictException =>
            new(StatusCodes.Status409Conflict, ErrorCodes.Conflict, exception.Message, LogLevel.Information),

        // ----- Lyrics service (LRCLIB) problems, after the provider's own retries -----

        LyricsServiceException { Failure: LyricsServiceFailure.Timeout } =>
            new(StatusCodes.Status504GatewayTimeout, ErrorCodes.LyricsTimeout,
                $"The lyrics service took too long to answer. {TryAgain}", LogLevel.Warning, TimeSpan.FromSeconds(5)),

        LyricsServiceException { Failure: LyricsServiceFailure.RateLimited } lyrics =>
            new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.LyricsUnavailable,
                $"The lyrics service is busy right now. {TryAgain}", LogLevel.Warning, lyrics.RetryAfter ?? TimeSpan.FromSeconds(10)),

        LyricsServiceException { Failure: LyricsServiceFailure.InvalidResponse } =>
            new(StatusCodes.Status502BadGateway, ErrorCodes.LyricsInvalidResponse,
                "The lyrics service sent an answer we couldn't read. Try again later.", LogLevel.Error),

        LyricsServiceException lyrics =>
            new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.LyricsUnavailable,
                $"The lyrics service (LRCLIB) is temporarily unavailable. {TryAgain}", LogLevel.Warning, lyrics.RetryAfter ?? TimeSpan.FromSeconds(5)),

        // ----- Database problems -----

        _ when IsDatabaseUnavailable(exception) =>
            new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.DatabaseUnavailable,
                $"The database is not reachable right now. {TryAgain}", LogLevel.Error, TimeSpan.FromSeconds(10)),

        TimeoutException =>
            new(StatusCodes.Status504GatewayTimeout, ErrorCodes.Timeout, $"That took too long. {TryAgain}", LogLevel.Warning),

        // ----- Everything else is a bug: log it fully, tell the player nothing internal -----

        _ => new(StatusCodes.Status500InternalServerError, ErrorCodes.Unexpected,
                 "Something went wrong on our side. Try again; if it keeps happening, report the trace id.", LogLevel.Error)
    };

    /// <summary>Connection or query failures, also when EF Core's retry strategy gave up. Not failed saves (those are bugs or conflicts).</summary>
    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DatabaseUnavailableException or RetryLimitExceededException
        || (exception is not DbUpdateException && Chain(exception).Any(e => e is DbException));

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    /// <summary>Fallback for responses that have a status code but no body (unknown route, wrong method...).</summary>
    public static ApiError FromStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => new(statusCode, ErrorCodes.Validation, "The request is not valid.", LogLevel.Information),
        StatusCodes.Status401Unauthorized => new(statusCode, ErrorCodes.Unauthorized, "You need to be logged in.", LogLevel.Information),
        StatusCodes.Status403Forbidden => new(statusCode, ErrorCodes.Forbidden, "You are not allowed to do this.", LogLevel.Information),
        StatusCodes.Status404NotFound => new(statusCode, ErrorCodes.NotFound, "This API endpoint does not exist.", LogLevel.Information),
        StatusCodes.Status405MethodNotAllowed => new(statusCode, ErrorCodes.MethodNotAllowed, "This HTTP method is not allowed here.", LogLevel.Information),
        StatusCodes.Status413PayloadTooLarge => new(statusCode, ErrorCodes.PayloadTooLarge, "The request is too large.", LogLevel.Information),
        StatusCodes.Status415UnsupportedMediaType => new(statusCode, ErrorCodes.UnsupportedMediaType, "Send the request body as JSON (Content-Type: application/json).", LogLevel.Information),
        StatusCodes.Status429TooManyRequests => new(statusCode, ErrorCodes.RateLimited, "Too many requests. Wait a minute and try again.", LogLevel.Information),
        StatusCodes.Status503ServiceUnavailable => new(statusCode, ErrorCodes.ServerUnavailable, "The server is temporarily unavailable. Try again in a moment.", LogLevel.Warning),
        >= 500 => new(statusCode, ErrorCodes.Unexpected, "Something went wrong on our side.", LogLevel.Error),
        _ => new(statusCode, ErrorCodes.Validation, "The request could not be handled.", LogLevel.Information)
    };
}
