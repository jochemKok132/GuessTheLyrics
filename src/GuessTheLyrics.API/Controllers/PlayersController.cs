using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Players;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController(IPlayerService playerService) : ControllerBase
{
    /// <summary>All players, ranked.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PlayerSummaryResponse>>>> GetLeaderboard(CancellationToken cancellationToken)
    {
        var players = await playerService.GetLeaderboardAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlayerSummaryResponse>>.Ok(players));
    }

    /// <summary>A player's statistics and every song they played.</summary>
    [HttpGet("{username}")]
    public async Task<ActionResult<ApiResponse<PlayerProfileResponse>>> GetProfile(string username, CancellationToken cancellationToken)
    {
        var profile = await playerService.GetProfileAsync(username, cancellationToken);
        return Ok(ApiResponse<PlayerProfileResponse>.Ok(profile));
    }
}
