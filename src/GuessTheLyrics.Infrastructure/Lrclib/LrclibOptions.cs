using System.ComponentModel.DataAnnotations;

namespace GuessTheLyrics.Infrastructure.Lrclib;

public sealed class LrclibOptions
{
    public const string SectionName = "Lrclib";

    [Required, Url]
    public required string BaseUrl { get; init; }

    [Required]
    public required string UserAgent { get; init; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Extra attempts after a temporary failure (5xx, 429, timeout, connection error).</summary>
    [Range(0, 5)]
    public int RetryCount { get; init; } = 2;

    /// <summary>Wait before the first retry; doubles per attempt. A Retry-After header from LRCLIB wins.</summary>
    [Range(0, 5000)]
    public int RetryBaseDelayMilliseconds { get; init; } = 300;

    /// <summary>Longest Retry-After we are willing to wait inside one request; longer means give up and tell the player.</summary>
    [Range(0, 30)]
    public int MaxRetryAfterSeconds { get; init; } = 3;
}
