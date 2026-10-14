using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Players;

namespace GuessTheLyrics.UI.Services;

public interface IPlayerService
{
    Task<ApiResponse<List<PlayerSummaryResponse>>> GetLeaderboardAsync();
    Task<ApiResponse<PlayerProfileResponse>> GetProfileAsync(string username);
}

public sealed class PlayerService(IHttpService http) : IPlayerService
{
    public Task<ApiResponse<List<PlayerSummaryResponse>>> GetLeaderboardAsync() =>
        http.GetAsync<List<PlayerSummaryResponse>>("api/players");

    public Task<ApiResponse<PlayerProfileResponse>> GetProfileAsync(string username) =>
        http.GetAsync<PlayerProfileResponse>($"api/players/{Uri.EscapeDataString(username)}");
}
