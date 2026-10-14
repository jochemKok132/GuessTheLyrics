namespace GuessTheLyrics.Shared;

/// <summary>Values of <see cref="ApiResponse{T}.ErrorCode"/>. The UI uses them to decide what to show and whether "Try again" makes sense.</summary>
public static class ErrorCodes
{
    // Caller mistakes (retrying the same request won't help)
    public const string Validation = "validation";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string PayloadTooLarge = "payload_too_large";

    // Temporary problems (retrying later can work)
    public const string RateLimited = "rate_limited";
    public const string LyricsUnavailable = "lyrics_unavailable";
    public const string LyricsTimeout = "lyrics_timeout";
    public const string LyricsInvalidResponse = "lyrics_invalid_response";
    public const string DatabaseUnavailable = "database_unavailable";
    public const string Timeout = "timeout";
    public const string Unexpected = "unexpected";

    // The caller went away before the answer was ready (never seen by a client)
    public const string ClientClosedRequest = "client_closed_request";

    // Only produced by the UI
    public const string Network = "network";
    public const string ServerUnavailable = "server_unavailable";

    private static readonly HashSet<string> Retryable =
    [
        RateLimited, LyricsUnavailable, LyricsTimeout, LyricsInvalidResponse, DatabaseUnavailable,
        Timeout, Unexpected, Network, ServerUnavailable
    ];

    /// <summary>True when trying the same request again later can succeed.</summary>
    public static bool IsRetryable(string? errorCode) => errorCode is not null && Retryable.Contains(errorCode);
}
