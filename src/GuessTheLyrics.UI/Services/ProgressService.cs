using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Progress;

namespace GuessTheLyrics.UI.Services;

public interface IProgressService
{
    Task<ApiResponse<SongProgressResponse?>> GetMineAsync(long songId);
    Task<ApiResponse<SongProgressResponse>> SaveAsync(long songId, SaveProgressRequest request);
    Task<ApiResponse<List<SongPlayerResponse>>> GetPlayersAsync(long songId);
}

public sealed class ProgressService(IHttpService http) : IProgressService
{
    public Task<ApiResponse<SongProgressResponse?>> GetMineAsync(long songId) =>
        http.GetAsync<SongProgressResponse?>($"api/progress/songs/{songId}");

    public Task<ApiResponse<SongProgressResponse>> SaveAsync(long songId, SaveProgressRequest request) =>
        http.PutAsync<SaveProgressRequest, SongProgressResponse>($"api/progress/songs/{songId}", request);

    public Task<ApiResponse<List<SongPlayerResponse>>> GetPlayersAsync(long songId) =>
        http.GetAsync<List<SongPlayerResponse>>($"api/progress/songs/{songId}/players");
}
