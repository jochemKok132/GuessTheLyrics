using GuessTheLyrics.Shared.Players;

namespace GuessTheLyrics.Application.Interfaces;

public interface IPlayerService
{
    Task<IReadOnlyList<PlayerSummaryResponse>> GetLeaderboardAsync(CancellationToken cancellationToken = default);
    Task<PlayerProfileResponse> GetProfileAsync(string username, CancellationToken cancellationToken = default);
}
