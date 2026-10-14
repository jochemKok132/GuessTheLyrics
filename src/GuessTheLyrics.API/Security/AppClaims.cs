namespace GuessTheLyrics.API.Security;

/// <summary>
/// Short JWT claim names. Inbound claim mapping is off, so the API reads exactly what it writes
/// and the Blazor UI can read the same names from the token.
/// </summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Username = "name";
    public const string Role = "role";
}
