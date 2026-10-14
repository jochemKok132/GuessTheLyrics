using System.Security.Authentication;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Validation;
using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Shared.Auth;

namespace GuessTheLyrics.Application.Services;

public sealed class AuthService(IUserRepository userRepository, IPassword password, ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();

        var username = request.Username.Trim();
        var normalized = AuthValidation.NormalizeUsername(username);

        if (await userRepository.ExistsAsync(normalized, cancellationToken))
            throw new ArgumentException("That username is already taken.");

        var user = new User
        {
            Username = username,
            NormalizedUsername = normalized,
            HashedPassword = password.Hash(request.Password)
        };

        await userRepository.AddAsync(user, cancellationToken);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();

        var user = await userRepository.GetByNormalizedUsernameAsync(AuthValidation.NormalizeUsername(request.Username), cancellationToken);

        // Same message for an unknown user and a wrong password, so usernames can't be probed.
        if (user is null || !password.Validate(request.Password, user.HashedPassword))
            throw new AuthenticationException("Invalid username or password.");

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user);

        return new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Username = user.Username
        };
    }
}
