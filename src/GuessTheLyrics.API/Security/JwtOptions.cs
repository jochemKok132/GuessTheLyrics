using System.ComponentModel.DataAnnotations;

namespace GuessTheLyrics.API.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key, at least 32 characters. Never commit a production key.</summary>
    [Required, MinLength(32)]
    public required string Key { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Range(5, 60 * 24 * 90)]
    public int ExpiryMinutes { get; init; } = 60 * 24 * 7;
}
