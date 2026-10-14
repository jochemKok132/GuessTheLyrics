using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Auth;
using GuessTheLyrics.UI.Auth;

namespace GuessTheLyrics.UI.Services;

public interface IAccountService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
}

public sealed class AccountService(IHttpService http, AuthStateProvider authStateProvider) : IAccountService
{
    public Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request) =>
        AuthenticateAsync(http.PostAsync<LoginRequest, AuthResponse>("api/auth/login", request));

    public Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request) =>
        AuthenticateAsync(http.PostAsync<RegisterRequest, AuthResponse>("api/auth/register", request));

    public Task LogoutAsync() => authStateProvider.MarkUserAsLoggedOutAsync();

    private async Task<ApiResponse<AuthResponse>> AuthenticateAsync(Task<ApiResponse<AuthResponse>> call)
    {
        var response = await call;
        if (response.Succeeded && response.Value is not null)
            await authStateProvider.MarkUserAsAuthenticatedAsync(response.Value.Token);
        return response;
    }
}
