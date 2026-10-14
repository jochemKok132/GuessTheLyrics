using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Mapping;
using GuessTheLyrics.Application.Validation;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;
using GuessTheLyrics.Shared.Players;

namespace GuessTheLyrics.Application.Services;

public sealed class PlayerService(IUserRepository userRepository, ISongProgressRepository progressRepository) : IPlayerService
{
    public async Task<IReadOnlyList<PlayerSummaryResponse>> GetLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await userRepository.GetPlayerSummariesAsync(cancellationToken);

        return Rank(summaries)
            .Select(x => x.Summary.ToResponse(x.Rank))
            .ToList();
    }

    public async Task<PlayerProfileResponse> GetProfileAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByNormalizedUsernameAsync(AuthValidation.NormalizeUsername(username), cancellationToken)
                   ?? throw new KeyNotFoundException("Player not found.");

        // The rank needs everyone's summary; fine for a hobby-sized player base.
        var ranked = Rank(await userRepository.GetPlayerSummariesAsync(cancellationToken));
        var mine = ranked.FirstOrDefault(x => x.Summary.UserId == user.Id);

        var summary = mine.Summary ?? new PlayerSummary(user.Id, user.Username, user.CreatedAt, 0, 0, 0, 0, null);
        var songs = await progressRepository.GetByUserAsync(user.Id, cancellationToken);

        return new PlayerProfileResponse
        {
            Summary = summary.ToResponse(mine.Rank),
            Songs = songs.Select(s => s.ToResponse(includeFoundKeys: false)).ToList()
        };
    }

    /// <summary>Most completed songs first, then the best average score, then the most words found.</summary>
    public static List<(PlayerSummary Summary, int Rank)> Rank(IEnumerable<PlayerSummary> summaries) =>
        summaries
            .OrderByDescending(s => s.SongsCompleted)
            .ThenByDescending(s => s.AverageBestPercentage)
            .ThenByDescending(s => s.WordsFound)
            .ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
            .Select((s, i) => (s, i + 1))
            .ToList();
}
