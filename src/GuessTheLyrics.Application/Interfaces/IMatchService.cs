using GuessTheLyrics.Shared.Matches;

namespace GuessTheLyrics.Application.Interfaces;

public interface IMatchService
{
    Task<MatchResponse> CreateAsync(Guid userId, string username, CreateMatchRequest request, CancellationToken cancellationToken = default);
    MatchResponse Get(string code, Guid userId);
    IReadOnlyList<MatchSummaryResponse> GetOpenMatches();
    MatchResponse Join(string code, Guid userId, string username);
    MatchResponse Leave(string code, Guid userId);
    MatchResponse Start(string code, Guid userId);
    MatchResponse SubmitProgress(string code, Guid userId, MatchProgressRequest request);
}
