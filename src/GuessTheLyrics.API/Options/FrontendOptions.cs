using System.ComponentModel.DataAnnotations;

namespace GuessTheLyrics.API.Options;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Origins of the Blazor UI that may call this API (CORS).</summary>
    [Required, MinLength(1)]
    public required string[] AllowedOrigins { get; init; }
}
