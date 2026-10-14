namespace GuessTheLyrics.Domain.Enums;

public enum ProgressStatus
{
    InProgress = 0,
    Completed = 1,
    GaveUp = 2,

    /// <summary>The countdown ran out before every word was found.</summary>
    TimeUp = 3
}
