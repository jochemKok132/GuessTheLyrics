using System.Diagnostics;

namespace GuessTheLyrics.API.Handlers;

public static class TraceIds
{
    /// <summary>The W3C trace id when tracing is on, otherwise ASP.NET Core's request id. Also appears in every log line of the request.</summary>
    public static string Get(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
