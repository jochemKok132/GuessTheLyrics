using GuessTheLyrics.Application.Lyrics;
using GuessTheLyrics.Domain.Models;
using GuessTheLyrics.Shared.Game;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Mapping;

public static class SongMappings
{
    public static SongSearchResultResponse ToSearchResultResponse(this LyricsTrack track) => new()
    {
        Id = track.Id,
        Title = track.TrackName,
        Artist = track.ArtistName,
        Album = track.AlbumName,
        DurationSeconds = (int)Math.Round(track.Duration),
        WordCount = LyricsTokenizer.CountWords(LyricsTokenizer.Tokenize(track.PlainLyrics))
    };

    public static LyricsPuzzleResponse ToPuzzleResponse(this LyricsTrack track)
    {
        var lines = LyricsTokenizer.Tokenize(track.PlainLyrics);
        var uniqueWords = LyricsTokenizer.CountUniqueWords(lines);

        return new LyricsPuzzleResponse
        {
            Id = track.Id,
            Title = track.TrackName,
            Artist = track.ArtistName,
            Album = track.AlbumName,
            TotalWords = LyricsTokenizer.CountWords(lines),
            UniqueWords = uniqueWords,
            TimeLimitSeconds = TimeLimits.ForUniqueWords(uniqueWords),
            Lines = lines
        };
    }
}
