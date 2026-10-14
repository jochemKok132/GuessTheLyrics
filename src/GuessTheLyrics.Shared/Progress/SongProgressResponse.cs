namespace GuessTheLyrics.Shared.Progress;

public sealed class SongProgressResponse
{
    public long SongId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public int TotalWords { get; init; }
    public int UniqueWords { get; init; }

    public int FoundWordCount { get; init; }
    public int Percentage { get; init; }
    public int ElapsedSeconds { get; init; }
    public ProgressStatusDto Status { get; init; }

    public int Attempts { get; init; }
    public int BestPercentage { get; init; }
    public int? BestTimeSeconds { get; init; }
    public DateTime LastPlayedAt { get; init; }

    /// <summary>Only filled in for your own progress, so a game can be resumed.</summary>
    public List<string>? FoundKeys { get; init; }
}
