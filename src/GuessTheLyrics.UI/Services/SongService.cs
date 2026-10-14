using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.UI.Services;

public sealed class SongService(IHttpService http) : ISongService
{
    public Task<ApiResponse<List<SongSearchResultResponse>>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        http.GetAsync<List<SongSearchResultResponse>>($"api/songs/search?query={Uri.EscapeDataString(query)}", cancellationToken);

    public Task<ApiResponse<LyricsPuzzleResponse>> GetPuzzleAsync(long id, CancellationToken cancellationToken = default) =>
        http.GetAsync<LyricsPuzzleResponse>($"api/songs/{id}/puzzle", cancellationToken);
}
