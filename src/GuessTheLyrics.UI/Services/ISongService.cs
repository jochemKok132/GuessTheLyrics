using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.UI.Services;

public interface ISongService
{
    Task<ApiResponse<List<SongSearchResultResponse>>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<ApiResponse<LyricsPuzzleResponse>> GetPuzzleAsync(long id, CancellationToken cancellationToken = default);
}
