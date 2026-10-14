namespace GuessTheLyrics.Domain.Exceptions;

public enum LyricsServiceFailure
{
    /// <summary>The lyrics service is down, overloaded (503) or answers with a server error.</summary>
    Unavailable,

    /// <summary>The lyrics service did not answer in time.</summary>
    Timeout,

    /// <summary>The lyrics service asked us to slow down (429).</summary>
    RateLimited,

    /// <summary>The lyrics service answered with something that isn't the expected JSON.</summary>
    InvalidResponse
}

/// <summary>The external lyrics service failed after retries. Temporary: the same request can work later.</summary>
public sealed class LyricsServiceException(LyricsServiceFailure failure, string message, TimeSpan? retryAfter = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public LyricsServiceFailure Failure { get; } = failure;

    /// <summary>How long the service asked us to wait, when it said so (Retry-After header).</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
