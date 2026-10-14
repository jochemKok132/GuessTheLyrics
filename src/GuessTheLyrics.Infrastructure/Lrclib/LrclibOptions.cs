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
}
