using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Players;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[Route("api/players")]
public sealed class PlayersController(IPlayerService playerService) : ErrorHandlingController
{
    /// <summary>All players, ranked.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PlayerSummaryResponse>>>> GetLeaderboard(CancellationToken cancellationToken)
        => await HandleAsync(() => playerService.GetLeaderboardAsync(cancellationToken));

    /// <summary>A player's statistics and every song they played.</summary>
    [HttpGet("{username}")]
    public async Task<ActionResult<ApiResponse<PlayerProfileResponse>>> GetProfile(string username, CancellationToken cancellationToken)
        => await HandleAsync(() => playerService.GetProfileAsync(username, cancellationToken));
}
