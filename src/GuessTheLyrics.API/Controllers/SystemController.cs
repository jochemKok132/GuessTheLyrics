using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[Route("api/system")]
public sealed class SystemController : ErrorHandlingController
{
    [HttpGet("version")]
    public ActionResult<ApiResponse<VersionResponse>> GetVersion() =>
        Handle(() => new VersionResponse { Version = AppVersion.Of(typeof(SystemController).Assembly) });
}
