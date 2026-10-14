using GuessTheLyrics.Application.Services;
using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;
using Moq;

namespace GuessTheLyrics.UnitTests;

public class PlayerServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ISongProgressRepository> _progressRepository = new();
    private readonly PlayerService _sut;

    public PlayerServiceTests()
    {
        _sut = new PlayerService(_userRepository.Object, _progressRepository.Object);
    }

    private static PlayerSummary Summary(string name, int completed, double average, int words = 0) =>
        new(Guid.NewGuid(), name, DateTime.UtcNow, completed + 1, completed, words, average, null);

    [Fact]
    public void Rank_OrdersByCompletedThenAverageThenWords()
    {
        var ranked = PlayerService.Rank(
        [
            Summary("c", completed: 1, average: 90),
            Summary("a", completed: 3, average: 50),
            Summary("b", completed: 1, average: 95),
            Summary("d", completed: 1, average: 90, words: 500)
        ]);

        Assert.Equal(new[] { "a", "b", "d", "c" }, ranked.Select(r => r.Summary.Username));
        Assert.Equal(new[] { 1, 2, 3, 4 }, ranked.Select(r => r.Rank));
    }

    [Fact]
    public async Task GetProfileAsync_UnknownPlayer_ThrowsKeyNotFoundException()
    {
        _userRepository.Setup(r => r.GetByNormalizedUsernameAsync("GHOST", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetProfileAsync("ghost"));
    }

    [Fact]
    public async Task GetProfileAsync_NewPlayerWithoutSongs_ReturnsEmptyProfile()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "newbie", NormalizedUsername = "NEWBIE", HashedPassword = "x" };
        _userRepository.Setup(r => r.GetByNormalizedUsernameAsync("NEWBIE", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepository.Setup(r => r.GetPlayerSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlayerSummary(user.Id, "newbie", DateTime.UtcNow, 0, 0, 0, 0, null)]);
        _progressRepository.Setup(r => r.GetByUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var profile = await _sut.GetProfileAsync("newbie");

        Assert.Equal("newbie", profile.Summary.Username);
        Assert.Equal(1, profile.Summary.Rank);
        Assert.Empty(profile.Songs);
    }
}
