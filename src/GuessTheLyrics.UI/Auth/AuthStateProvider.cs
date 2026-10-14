using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GuessTheLyrics.UI.Auth;

public sealed class AuthStateProvider(ILocalStorageService storage) : AuthenticationStateProvider
{
    private const string TokenKey = "guessthelyrics.token";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetTokenAsync();
        return token is null ? Anonymous : StateFor(token);
    }

    /// <returns>The stored token, or <c>null</c> when there is none or it has expired.</returns>
    public async Task<string?> GetTokenAsync()
    {
        string? token;
        try
        {
            token = await storage.GetAsync(TokenKey);
        }
        catch
        {
            return null; // Storage blocked (private mode etc.): behave as logged out.
        }

        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (ParseJwt.IsExpired(ParseJwt.GetClaims(token)))
        {
            await storage.RemoveAsync(TokenKey);
            return null;
        }

        return token;
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        await storage.SetAsync(TokenKey, token);
        NotifyAuthenticationStateChanged(Task.FromResult(StateFor(token)));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await storage.RemoveAsync(TokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static AuthenticationState StateFor(string token)
    {
        // Claim names match the API's AppClaims: "sub", "name", "role".
        var identity = new ClaimsIdentity(ParseJwt.GetClaims(token), "jwt", "name", "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
