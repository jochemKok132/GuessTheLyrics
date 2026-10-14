using GuessTheLyrics.API.Extensions;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

/// <summary>Multiplayer matches. Clients poll GET {code} about once a second while a match runs.</summary>
[Authorize]
[Route("api/matches")]
public sealed class MatchesController(IMatchService matchService) : ErrorHandlingController
{
    /// <summary>Open lobbies you can join, newest first.</summary>
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<MatchSummaryResponse>>> GetOpen() =>
        Handle(matchService.GetOpenMatches);

    /// <summary>Creates a match for a song; you are the host.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MatchResponse>>> Create(CreateMatchRequest request, CancellationToken cancellationToken)
        => await HandleAsync(() => matchService.CreateAsync(User.GetUserId(), User.GetUsername(), request, cancellationToken));

    [HttpGet("{code}")]
    public ActionResult<ApiResponse<MatchResponse>> Get(string code) =>
        Handle(() => matchService.Get(code, User.GetUserId()));

    [HttpPost("{code}/join")]
    public ActionResult<ApiResponse<MatchResponse>> Join(string code) =>
        Handle(() => matchService.Join(code, User.GetUserId(), User.GetUsername()));

    [HttpPost("{code}/leave")]
    public ActionResult<ApiResponse<MatchResponse>> Leave(string code) =>
        Handle(() => matchService.Leave(code, User.GetUserId()));

    /// <summary>Host only: starts the countdown.</summary>
    [HttpPost("{code}/start")]
    public ActionResult<ApiResponse<MatchResponse>> Start(string code) =>
        Handle(() => matchService.Start(code, User.GetUserId()));

    /// <summary>Sends the words you found; answers with the live scoreboard.</summary>
    [HttpPut("{code}/progress")]
    public ActionResult<ApiResponse<MatchResponse>> SubmitProgress(string code, MatchProgressRequest request) =>
        Handle(() => matchService.SubmitProgress(code, User.GetUserId(), request));
}
