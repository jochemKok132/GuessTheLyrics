namespace GuessTheLyrics.Shared.Songs;

public sealed class SongSearchRequest
{
    /// <summary>Free text: song title, artist, or both (e.g. "bloodhound gang the bad touch").</summary>
    public string Query { get; set; } = string.Empty;
}
