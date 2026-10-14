using System.Text.RegularExpressions;
using GuessTheLyrics.Shared.Auth;

namespace GuessTheLyrics.Application.Validation;

public static partial class AuthValidation
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 20;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 100;

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex UsernameRegex();

    public static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToUpperInvariant();

    public static void Validate(this RegisterRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;

        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
            throw new ArgumentException($"A username must be {MinUsernameLength} to {MaxUsernameLength} characters.");

        if (!UsernameRegex().IsMatch(username))
            throw new ArgumentException("A username can only contain letters, digits, '-' and '_'.");

        var password = request.Password ?? string.Empty;

        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"A password must be at least {MinPasswordLength} characters.");

        if (password.Length > MaxPasswordLength)
            throw new ArgumentException($"A password can be at most {MaxPasswordLength} characters.");
    }

    public static void Validate(this LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            throw new ArgumentException("Enter your username and password.");
    }
}
