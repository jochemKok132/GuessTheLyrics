using GuessTheLyrics.Application.Caching;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Mapping;
using GuessTheLyrics.Application.Validation;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Services;

public sealed class SongService(ILyricsProvider lyricsProvider, IPuzzleCache puzzleCache) : ISongService
{
    public const int MaxResults = 20;

    public async Task<IReadOnlyList<SongSearchResultResponse>> SearchAsync(SongSearchRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();

        var tracks = await lyricsProvider.SearchAsync(request.Query.Trim(), cancellationToken);

        // The lyrics database often holds the same song several times (single, album, live...).
        // Keep the first hit per artist + title so the list stays readable.
        return tracks
            .Where(t => t.HasLyrics)
            .DistinctBy(t => (WordNormalizer.Normalize(t.ArtistName), WordNormalizer.Normalize(t.TrackName)))
            .Take(MaxResults)
            .Select(t => t.ToSearchResultResponse())
            .ToList();
    }

    public async Task<LyricsPuzzleResponse> GetPuzzleAsync(long id, CancellationToken cancellationToken = default)
    {
        SongValidation.ValidateSongId(id);

        if (puzzleCache.TryGet(id, out var cached))
            return cached;

        var track = await lyricsProvider.GetByIdAsync(id, cancellationToken)
                    ?? throw new KeyNotFoundException("Song not found.");

        if (!track.HasLyrics)
            throw new KeyNotFoundException("This song has no lyrics to guess (it may be instrumental).");

        var puzzle = track.ToPuzzleResponse();
        puzzleCache.Set(id, puzzle);
        return puzzle;
    }
}
