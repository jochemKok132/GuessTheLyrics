using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Lyrics;
using GuessTheLyrics.Application.Matches;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Shared.Matches;
using GuessTheLyrics.Shared.Songs;
using Moq;

namespace GuessTheLyrics.UnitTests;

public class MatchServiceTests
{
    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private const long SongId = 7;
    private readonly ManualTime _time = new();
    private readonly Mock<ISongService> _songService = new();
    private readonly MatchStore _store = new();
    private readonly MatchService _sut;

    private readonly Guid _host = Guid.NewGuid();
    private readonly Guid _guest = Guid.NewGuid();

    public MatchServiceTests()
    {
        // "hi hi there" → 3 words, 2 unique, time limit 60 s
        var lines = LyricsTokenizer.Tokenize("hi hi there");
        _songService.Setup(s => s.GetPuzzleAsync(SongId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LyricsPuzzleResponse
            {
                Id = SongId,
                Title = "Song",
                Artist = "Artist",
                Lines = lines,
                TotalWords = 3,
                UniqueWords = 2,
                TimeLimitSeconds = 60
            });

        _sut = new MatchService(_store, _songService.Object, _time);
    }

    private async Task<string> CreateStartedMatchAsync()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        _sut.Join(match.Code, _guest, "guest");
        _sut.Start(match.Code, _host);
        _time.Advance(TimeSpan.FromSeconds(MatchService.CountdownSeconds));
        return match.Code;
    }

    [Fact]
    public async Task CreateAsync_ValidSong_CreatesLobbyWithHost()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });

        Assert.Equal(MatchStatusDto.Lobby, match.Status);
        Assert.Equal(6, match.Code.Length);
        Assert.True(match.YouAreHost);
        Assert.Equal(60, match.TimeLimitSeconds);
        Assert.Single(match.Players);
    }

    [Fact]
    public async Task Join_SecondPlayer_IsAddedOnce()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });

        _sut.Join(match.Code, _guest, "guest");
        var again = _sut.Join(match.Code.ToLowerInvariant(), _guest, "guest");

        Assert.Equal(2, again.Players.Count);
        Assert.True(again.YouAreIn);
    }

    [Fact]
    public async Task Join_StartedMatch_ThrowsConflictException()
    {
        var code = await CreateStartedMatchAsync();

        Assert.Throws<ConflictException>(() => _sut.Join(code, Guid.NewGuid(), "late"));
    }

    [Fact]
    public async Task Join_FullMatch_ThrowsConflictException()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        for (var i = 1; i < MatchService.MaxPlayers; i++)
            _sut.Join(match.Code, Guid.NewGuid(), $"p{i}");

        Assert.Throws<ConflictException>(() => _sut.Join(match.Code, Guid.NewGuid(), "one-too-many"));
    }

    [Fact]
    public void Get_UnknownCode_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => _sut.Get("ABCDEF", _host));
    }

    [Fact]
    public void Get_InvalidCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.Get("no!", _host));
    }

    [Fact]
    public async Task Start_ByGuest_ThrowsUnauthorizedAccessException()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        _sut.Join(match.Code, _guest, "guest");

        Assert.Throws<UnauthorizedAccessException>(() => _sut.Start(match.Code, _guest));
    }

    [Fact]
    public async Task Start_ByHost_StartsCountdownThenPlaying()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });

        var started = _sut.Start(match.Code, _host);
        _time.Advance(TimeSpan.FromSeconds(MatchService.CountdownSeconds));
        var playing = _sut.Get(match.Code, _host);

        Assert.Equal(MatchStatusDto.Countdown, started.Status);
        Assert.Equal(MatchStatusDto.Playing, playing.Status);
        Assert.Equal(started.StartsAt!.Value.AddSeconds(60), started.EndsAt);
    }

    [Fact]
    public async Task SubmitProgress_DuringCountdown_ThrowsArgumentException()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        _sut.Start(match.Code, _host);

        Assert.Throws<ArgumentException>(() =>
            _sut.SubmitProgress(match.Code, _host, new MatchProgressRequest { FoundKeys = ["hi"] }));
    }

    [Fact]
    public async Task SubmitProgress_RealAndFakeWords_CountsOnlyRealWords()
    {
        var code = await CreateStartedMatchAsync();

        var board = _sut.SubmitProgress(code, _guest, new MatchProgressRequest { FoundKeys = ["hi", "fake"] });

        var guest = board.Players.Single(p => p.IsYou);
        Assert.Equal(2, guest.FoundWordCount);
        Assert.Equal(1, guest.Rank);
    }

    [Fact]
    public async Task SubmitProgress_FirstToFindEverything_WinsAndMatchEndsWhenAllDone()
    {
        var code = await CreateStartedMatchAsync();

        _time.Advance(TimeSpan.FromSeconds(10));
        _sut.SubmitProgress(code, _guest, new MatchProgressRequest { FoundKeys = ["hi", "there"] });
        _time.Advance(TimeSpan.FromSeconds(10));
        var board = _sut.SubmitProgress(code, _host, new MatchProgressRequest { FoundKeys = ["hi", "there"] });

        Assert.Equal(MatchStatusDto.Finished, board.Status);
        Assert.Equal("guest", board.WinnerUsername);
        Assert.Equal(10, board.Players.Single(p => p.Username == "guest").FinishedAfterSeconds);
    }

    [Fact]
    public async Task Get_AfterTimeLimit_IsFinishedAndMostWordsWins()
    {
        var code = await CreateStartedMatchAsync();
        _sut.SubmitProgress(code, _host, new MatchProgressRequest { FoundKeys = ["there"] });
        _sut.SubmitProgress(code, _guest, new MatchProgressRequest { FoundKeys = ["hi"] });

        _time.Advance(TimeSpan.FromSeconds(61));
        var board = _sut.Get(code, _host);

        Assert.Equal(MatchStatusDto.Finished, board.Status);
        Assert.Equal("guest", board.WinnerUsername); // "hi" counts twice
    }

    [Fact]
    public async Task SubmitProgress_LongAfterTheEnd_IsIgnored()
    {
        var code = await CreateStartedMatchAsync();

        _time.Advance(TimeSpan.FromSeconds(120));
        var board = _sut.SubmitProgress(code, _guest, new MatchProgressRequest { FoundKeys = ["hi", "there"] });

        Assert.Equal(0, board.Players.Single(p => p.IsYou).FoundWordCount);
    }

    [Fact]
    public async Task Leave_HostLeavesLobby_NextPlayerBecomesHost()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        _sut.Join(match.Code, _guest, "guest");

        _sut.Leave(match.Code, _host);
        var board = _sut.Get(match.Code, _guest);

        Assert.True(board.YouAreHost);
        Assert.Single(board.Players);
    }

    [Fact]
    public async Task Leave_LastPlayerLeavesLobby_RemovesMatch()
    {
        var match = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });

        _sut.Leave(match.Code, _host);

        Assert.Throws<KeyNotFoundException>(() => _sut.Get(match.Code, _host));
    }

    [Fact]
    public async Task Leave_DuringRace_KeepsScoreAndEndsWhenOthersAreDone()
    {
        var code = await CreateStartedMatchAsync();
        _sut.SubmitProgress(code, _guest, new MatchProgressRequest { FoundKeys = ["hi"] });

        _sut.Leave(code, _guest);
        var board = _sut.SubmitProgress(code, _host, new MatchProgressRequest { FoundKeys = ["hi", "there"] });

        Assert.Equal(MatchStatusDto.Finished, board.Status);
        Assert.True(board.Players.Single(p => p.Username == "guest").Left);
        Assert.Equal("host", board.WinnerUsername);
    }

    [Fact]
    public async Task GetOpenMatches_OnlyLobbies()
    {
        var open = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });
        await CreateStartedMatchAsync();

        var list = _sut.GetOpenMatches();

        Assert.Equal(open.Code, Assert.Single(list).Code);
    }

    [Fact]
    public async Task RemoveExpired_OldLobbyAndFinishedMatch_AreRemoved()
    {
        var lobby = await _sut.CreateAsync(_host, "host", new CreateMatchRequest { SongId = SongId });

        _time.Advance(TimeSpan.FromHours(2));
        var removed = _store.RemoveExpired(_time.GetUtcNow().UtcDateTime);

        Assert.Equal(1, removed);
        Assert.Throws<KeyNotFoundException>(() => _sut.Get(lobby.Code, _host));
    }
}
