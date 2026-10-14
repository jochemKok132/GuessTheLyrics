namespace GuessTheLyrics.Domain.Entities;

public sealed class User : BaseEntity
{
    /// <summary>Username as the player typed it (shown in the UI).</summary>
    public required string Username { get; set; }

    /// <summary>Upper-case username, unique, used for lookups so "Jochem" and "jochem" are the same account.</summary>
    public required string NormalizedUsername { get; set; }

    public required string HashedPassword { get; set; }

    public ICollection<SongProgress> Progress { get; set; } = [];
}
