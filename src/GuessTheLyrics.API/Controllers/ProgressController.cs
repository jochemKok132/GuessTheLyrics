using GuessTheLyrics.API.Extensions;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[ApiController]
[Authorize]
[Route("api/progress/songs/{songId:long}")]
public sealed class ProgressController(IProgressService progressService) : ControllerBase
{
    /// <summary>Your progress on a song; <c>value</c> is empty when you never played it.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SongProgressResponse?>>> GetMine(long songId, CancellationToken cancellationToken)
    {
        var progress = await progressService.GetMyProgressAsync(User.GetUserId(), songId, cancellationToken);
        return Ok(ApiResponse<SongProgressResponse?>.Ok(progress));
    }

    /// <summary>Saves the current attempt. Send an empty list to start over.</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<SongProgressResponse>>> Save(long songId, SaveProgressRequest request, CancellationToken cancellationToken)
    {
        var progress = await progressService.SaveAsync(User.GetUserId(), songId, request, cancellationToken);
        return Ok(ApiResponse<SongProgressResponse>.Ok(progress));
    }

    /// <summary>Every player's progress on a song.</summary>
    [AllowAnonymous]
    [HttpGet("players")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SongPlayerResponse>>>> GetPlayers(long songId, CancellationToken cancellationToken)
    {
        var players = await progressService.GetSongPlayersAsync(songId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SongPlayerResponse>>.Ok(players));
    }
}
