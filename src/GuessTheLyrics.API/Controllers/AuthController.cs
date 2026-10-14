using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GuessTheLyrics.API.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ErrorHandlingController
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request, CancellationToken cancellationToken)
        => await HandleAsync(() => authService.RegisterAsync(request, cancellationToken), "Account created.");

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
        => await HandleAsync(() => authService.LoginAsync(request, cancellationToken));
}
