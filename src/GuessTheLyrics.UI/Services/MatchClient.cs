using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Matches;

namespace GuessTheLyrics.UI.Services;

public interface IMatchClient
{
    Task<ApiResponse<List<MatchSummaryResponse>>> GetOpenAsync();
    Task<ApiResponse<MatchResponse>> CreateAsync(long songId);
    Task<ApiResponse<MatchResponse>> GetAsync(string code);
    Task<ApiResponse<MatchResponse>> JoinAsync(string code);
    Task<ApiResponse<MatchResponse>> LeaveAsync(string code);
    Task<ApiResponse<MatchResponse>> StartAsync(string code);
    Task<ApiResponse<MatchResponse>> SubmitProgressAsync(string code, IEnumerable<string> foundKeys);
}

public sealed class MatchClient(IHttpService http) : IMatchClient
{
    public Task<ApiResponse<List<MatchSummaryResponse>>> GetOpenAsync() =>
        http.GetAsync<List<MatchSummaryResponse>>("api/matches");

    public Task<ApiResponse<MatchResponse>> CreateAsync(long songId) =>
        http.PostAsync<CreateMatchRequest, MatchResponse>("api/matches", new CreateMatchRequest { SongId = songId });

    public Task<ApiResponse<MatchResponse>> GetAsync(string code) =>
        http.GetAsync<MatchResponse>($"api/matches/{Uri.EscapeDataString(code)}");

    public Task<ApiResponse<MatchResponse>> JoinAsync(string code) =>
        http.PostAsync<object?, MatchResponse>($"api/matches/{Uri.EscapeDataString(code)}/join", null);

    public Task<ApiResponse<MatchResponse>> LeaveAsync(string code) =>
        http.PostAsync<object?, MatchResponse>($"api/matches/{Uri.EscapeDataString(code)}/leave", null);

    public Task<ApiResponse<MatchResponse>> StartAsync(string code) =>
        http.PostAsync<object?, MatchResponse>($"api/matches/{Uri.EscapeDataString(code)}/start", null);

    public Task<ApiResponse<MatchResponse>> SubmitProgressAsync(string code, IEnumerable<string> foundKeys) =>
        http.PutAsync<MatchProgressRequest, MatchResponse>(
            $"api/matches/{Uri.EscapeDataString(code)}/progress",
            new MatchProgressRequest { FoundKeys = foundKeys.ToList() });
}
