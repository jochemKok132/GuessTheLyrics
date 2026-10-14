using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("version")]
    public ActionResult<ApiResponse<VersionResponse>> GetVersion() =>
        Ok(ApiResponse<VersionResponse>.Ok(new VersionResponse
        {
            Version = AppVersion.Of(typeof(SystemController).Assembly)
        }));
}
