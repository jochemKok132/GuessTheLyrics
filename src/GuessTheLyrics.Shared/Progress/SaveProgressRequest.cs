namespace GuessTheLyrics.Shared.Progress;

public sealed class SaveProgressRequest
{
    /// <summary>Normalised words found in the current attempt.</summary>
    public List<string> FoundKeys { get; set; } = [];

    public int ElapsedSeconds { get; set; }

    /// <summary>
    /// <see cref="ProgressStatusDto.InProgress"/> while playing (an empty list starts a new attempt) or
    /// <see cref="ProgressStatusDto.GaveUp"/>. Completed is decided by the server when every word is found.
    /// </summary>
    public ProgressStatusDto Status { get; set; } = ProgressStatusDto.InProgress;
}
