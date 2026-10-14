namespace GuessTheLyrics.Shared.Progress;

public sealed class SaveProgressRequest
{
    /// <summary>Normalised words found in the current attempt.</summary>
    public List<string> FoundKeys { get; set; } = [];

    public int ElapsedSeconds { get; set; }

    /// <summary>
    /// <see cref="ProgressStatusDto.InProgress"/> while playing (an empty list starts a new attempt),
    /// <see cref="ProgressStatusDto.GaveUp"/> or <see cref="ProgressStatusDto.TimeUp"/>. Completed is decided by the
    /// server when every word is found within the time limit.
    /// </summary>
    public ProgressStatusDto Status { get; set; } = ProgressStatusDto.InProgress;
}
