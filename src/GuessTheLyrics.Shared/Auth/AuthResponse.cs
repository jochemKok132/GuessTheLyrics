namespace GuessTheLyrics.Shared.Auth;

public sealed class AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
