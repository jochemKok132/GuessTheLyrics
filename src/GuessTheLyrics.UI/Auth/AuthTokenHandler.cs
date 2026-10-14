using System.Net;
using System.Net.Http.Headers;

namespace GuessTheLyrics.UI.Auth;

/// <summary>Adds the bearer token to API calls and logs out when the API no longer accepts it.</summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthStateProvider _authStateProvider;

    public AuthTokenHandler(AuthStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authStateProvider.GetTokenAsync();
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (token is not null && response.StatusCode == HttpStatusCode.Unauthorized)
            await _authStateProvider.MarkUserAsLoggedOutAsync();

        return response;
    }
}
