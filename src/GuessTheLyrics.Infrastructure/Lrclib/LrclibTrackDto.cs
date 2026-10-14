namespace GuessTheLyrics.Infrastructure.Lrclib;

/// <summary>
/// Track as returned by https://lrclib.net/api/search and /api/get/{id}.
/// Everything except the id can be null in LRCLIB's data, so all fields are nullable.
/// </summary>
internal sealed class LrclibTrackDto
{
    public long Id { get; init; }
    public string? TrackName { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public double? Duration { get; init; }
    public bool? Instrumental { get; init; }
    public string? PlainLyrics { get; init; }
}
