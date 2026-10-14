using System.Security.Authentication;
using System.Security.Claims;
using GuessTheLyrics.API.Security;

namespace GuessTheLyrics.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(AppClaims.UserId), out var id)
            ? id
            : throw new AuthenticationException("You need to be logged in.");

    public static string GetUsername(this ClaimsPrincipal user) =>
        user.FindFirstValue(AppClaims.Username) is { Length: > 0 } name
            ? name
            : throw new AuthenticationException("You need to be logged in.");
}
