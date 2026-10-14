using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Songs;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[Route("api/songs")]
public sealed class SongsController(ISongService songService) : ErrorHandlingController
{
    /// <summary>Searches songs by title and/or artist. Only songs with lyrics are returned.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SongSearchResultResponse>>>> Search(
        [FromQuery] SongSearchRequest request, CancellationToken cancellationToken)
        => await HandleAsync(() => songService.SearchAsync(request, cancellationToken));

    /// <summary>Gets a song's lyrics split into guessable words.</summary>
    [HttpGet("{id:long}/puzzle")]
    public async Task<ActionResult<ApiResponse<LyricsPuzzleResponse>>> GetPuzzle(long id, CancellationToken cancellationToken)
        => await HandleAsync(() => songService.GetPuzzleAsync(id, cancellationToken));
}
