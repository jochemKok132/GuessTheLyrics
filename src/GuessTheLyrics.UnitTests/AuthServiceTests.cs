using System.Security.Authentication;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Services;
using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Shared.Auth;
using Moq;

namespace GuessTheLyrics.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPassword> _password = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _password.Setup(p => p.Hash(It.IsAny<string>())).Returns<string>(pw => "hashed:" + pw);
        _password.Setup(p => p.Validate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((pw, hash) => hash == "hashed:" + pw);
        _tokenService.Setup(t => t.CreateToken(It.IsAny<User>()))
            .Returns(("token", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        _sut = new AuthService(_userRepository.Object, _password.Object, _tokenService.Object);
    }

    private static User ExistingUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "Jochem",
        NormalizedUsername = "JOCHEM",
        HashedPassword = "hashed:secret123"
    };

    [Theory]
    [InlineData("ab", "password123")]
    [InlineData("this-name-is-way-too-long", "password123")]
    [InlineData("bad name", "password123")]
    [InlineData("jochem", "short")]
    public async Task RegisterAsync_InvalidInput_ThrowsArgumentException(string username, string password)
    {
        var request = new RegisterRequest { Username = username, Password = password };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RegisterAsync(request));

        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_UsernameTaken_ThrowsConflictException()
    {
        _userRepository.Setup(r => r.ExistsAsync("JOCHEM", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new RegisterRequest { Username = "jochem", Password = "password123" };

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_SameNameRegisteredAtTheSameTime_ThrowsConflictWithUsernameMessage()
    {
        _userRepository.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("This already exists."));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest { Username = "jochem", Password = "password123" }));

        Assert.Equal("That username is already taken.", exception.Message);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_StoresHashedPasswordAndReturnsToken()
    {
        User? captured = null;
        _userRepository.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);

        var response = await _sut.RegisterAsync(new RegisterRequest { Username = " Jochem ", Password = "password123" });

        Assert.NotNull(captured);
        Assert.Equal("Jochem", captured!.Username);
        Assert.Equal("JOCHEM", captured.NormalizedUsername);
        Assert.Equal("hashed:password123", captured.HashedPassword);
        Assert.Equal("token", response.Token);
        Assert.Equal("Jochem", response.Username);
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_ThrowsAuthenticationException()
    {
        _userRepository.Setup(r => r.GetByNormalizedUsernameAsync("NOBODY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            _sut.LoginAsync(new LoginRequest { Username = "nobody", Password = "whatever1" }));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsAuthenticationException()
    {
        _userRepository.Setup(r => r.GetByNormalizedUsernameAsync("JOCHEM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            _sut.LoginAsync(new LoginRequest { Username = "jochem", Password = "wrong-password" }));
    }

    [Fact]
    public async Task LoginAsync_CorrectPasswordAnyCase_ReturnsToken()
    {
        _userRepository.Setup(r => r.GetByNormalizedUsernameAsync("JOCHEM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser());

        var response = await _sut.LoginAsync(new LoginRequest { Username = "jOcHeM", Password = "secret123" });

        Assert.Equal("token", response.Token);
        Assert.Equal("Jochem", response.Username);
    }
}
