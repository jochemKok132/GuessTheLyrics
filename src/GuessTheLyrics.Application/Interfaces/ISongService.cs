using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Interfaces;

public interface ISongService
{
    Task<IReadOnlyList<SongSearchResultResponse>> SearchAsync(SongSearchRequest request, CancellationToken cancellationToken = default);

    Task<LyricsPuzzleResponse> GetPuzzleAsync(long id, CancellationToken cancellationToken = default);
}
