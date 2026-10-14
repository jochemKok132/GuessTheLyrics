namespace GuessTheLyrics.Domain.Models;

/// <summary>A song with its plain-text lyrics, as returned by a lyrics provider.</summary>
public sealed class LyricsTrack
{
    public required long Id { get; init; }
    public required string TrackName { get; init; }
    public required string ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public double Duration { get; init; }
    public bool Instrumental { get; init; }
    public string? PlainLyrics { get; init; }

    public bool HasLyrics => !Instrumental && !string.IsNullOrWhiteSpace(PlainLyrics);
}
