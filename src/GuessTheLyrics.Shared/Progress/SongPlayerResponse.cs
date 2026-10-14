namespace GuessTheLyrics.Shared.Progress;

/// <summary>How one player is doing on a specific song.</summary>
public sealed class SongPlayerResponse
{
    public string Username { get; init; } = string.Empty;
    public int Percentage { get; init; }
    public ProgressStatusDto Status { get; init; }
    public int BestPercentage { get; init; }
    public int? BestTimeSeconds { get; init; }
    public int Attempts { get; init; }
    public DateTime LastPlayedAt { get; init; }
}
