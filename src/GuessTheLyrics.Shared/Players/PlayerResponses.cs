using GuessTheLyrics.Shared.Progress;

namespace GuessTheLyrics.Shared.Players;

public sealed class PlayerSummaryResponse
{
    public int Rank { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public int SongsPlayed { get; init; }
    public int SongsCompleted { get; init; }
    public int WordsFound { get; init; }
    public int AverageBestPercentage { get; init; }
    public DateTime? LastPlayedAt { get; init; }
}

public sealed class PlayerProfileResponse
{
    public PlayerSummaryResponse Summary { get; init; } = new();
    public List<SongProgressResponse> Songs { get; init; } = [];
}
