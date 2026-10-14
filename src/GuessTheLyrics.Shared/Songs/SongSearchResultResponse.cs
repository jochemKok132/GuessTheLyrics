namespace GuessTheLyrics.Shared.Songs;

public sealed class SongSearchResultResponse
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public int DurationSeconds { get; init; }
    public int WordCount { get; init; }
}
