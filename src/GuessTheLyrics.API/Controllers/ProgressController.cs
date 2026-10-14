using GuessTheLyrics.API.Extensions;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[Authorize]
[Route("api/progress/songs/{songId:long}")]
public sealed class ProgressController(IProgressService progressService) : ErrorHandlingController
{
    /// <summary>Your progress on a song; <c>value</c> is empty when you never played it.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SongProgressResponse?>>> GetMine(long songId, CancellationToken cancellationToken)
        => await HandleAsync(() => progressService.GetMyProgressAsync(User.GetUserId(), songId, cancellationToken));

    /// <summary>Saves the current attempt. Send an empty list to start over.</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<SongProgressResponse>>> Save(long songId, SaveProgressRequest request, CancellationToken cancellationToken)
        => await HandleAsync(() => progressService.SaveAsync(User.GetUserId(), songId, request, cancellationToken));

    /// <summary>Every player's progress on a song.</summary>
    [AllowAnonymous]
    [HttpGet("players")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SongPlayerResponse>>>> GetPlayers(long songId, CancellationToken cancellationToken)
        => await HandleAsync(() => progressService.GetSongPlayersAsync(songId, cancellationToken));
}
