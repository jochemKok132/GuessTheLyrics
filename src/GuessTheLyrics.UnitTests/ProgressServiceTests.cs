using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Lyrics;
using GuessTheLyrics.Application.Services;
using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Enums;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Shared.Progress;
using GuessTheLyrics.Shared.Songs;
using Moq;

namespace GuessTheLyrics.UnitTests;

public class ProgressServiceTests
{
    private const long SongId = 42;

    private readonly Mock<ISongProgressRepository> _progressRepository = new();
    private readonly Mock<ISongService> _songService = new();
    private readonly ProgressService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public ProgressServiceTests()
    {
        // "na na na hey" → 4 words, 2 unique (na ×3, hey ×1)
        var lines = LyricsTokenizer.Tokenize("na na na hey");
        _songService.Setup(s => s.GetPuzzleAsync(SongId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LyricsPuzzleResponse
            {
                Id = SongId,
                Title = "Song",
                Artist = "Artist",
                TotalWords = LyricsTokenizer.CountWords(lines),
                UniqueWords = LyricsTokenizer.CountUniqueWords(lines),
                Lines = lines
            });

        _sut = new ProgressService(_progressRepository.Object, _songService.Object);
    }

    private SongProgress Existing(ProgressStatus status, int bestPercentage = 0, int? bestTime = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        SongId = SongId,
        Title = "Song",
        Artist = "Artist",
        TotalWords = 4,
        UniqueWords = 2,
        Status = status,
        BestPercentage = bestPercentage,
        BestTimeSeconds = bestTime
    };

    private void SetupExisting(SongProgress? progress) =>
        _progressRepository.Setup(r => r.GetAsync(_userId, SongId, It.IsAny<CancellationToken>())).ReturnsAsync(progress);

    [Fact]
    public async Task SaveAsync_FirstSave_AddsProgressWithCountsFromLyrics()
    {
        SetupExisting(null);
        SongProgress? added = null;
        _progressRepository.Setup(r => r.AddAsync(It.IsAny<SongProgress>(), It.IsAny<CancellationToken>()))
            .Callback<SongProgress, CancellationToken>((p, _) => added = p)
            .Returns(Task.CompletedTask);

        var result = await _sut.SaveAsync(_userId, SongId, new SaveProgressRequest { FoundKeys = ["na"], ElapsedSeconds = 12 });

        Assert.NotNull(added);
        Assert.Equal(3, added!.FoundWordCount);
        Assert.Equal(75, result.Percentage);
        Assert.Equal(75, result.BestPercentage);
        Assert.Equal(ProgressStatusDto.InProgress, result.Status);
        Assert.Equal("Song", added.Title);
    }

    [Fact]
    public async Task SaveAsync_UnknownAndDuplicateKeys_AreIgnored()
    {
        SetupExisting(null);

        var result = await _sut.SaveAsync(_userId, SongId,
            new SaveProgressRequest { FoundKeys = ["hey", "hey", "notinthesong"], ElapsedSeconds = 5 });

        Assert.Equal(1, result.FoundWordCount);
        Assert.Equal(new List<string> { "hey" }, result.FoundKeys);
    }

    [Fact]
    public async Task SaveAsync_AllWordsFound_CompletesAndRecordsBestTime()
    {
        var existing = Existing(ProgressStatus.InProgress);
        SetupExisting(existing);

        var result = await _sut.SaveAsync(_userId, SongId,
            new SaveProgressRequest { FoundKeys = ["na", "hey"], ElapsedSeconds = 30 });

        Assert.Equal(ProgressStatusDto.Completed, result.Status);
        Assert.Equal(100, result.BestPercentage);
        Assert.Equal(30, result.BestTimeSeconds);
        Assert.Equal(1, result.Attempts);
        _progressRepository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_SlowerCompletion_KeepsFastestTime()
    {
        SetupExisting(Existing(ProgressStatus.InProgress, bestPercentage: 100, bestTime: 20));

        var result = await _sut.SaveAsync(_userId, SongId,
            new SaveProgressRequest { FoundKeys = ["na", "hey"], ElapsedSeconds = 50 });

        Assert.Equal(20, result.BestTimeSeconds);
    }

    [Fact]
    public async Task SaveAsync_FinishedGameSavedAgain_DoesNotCountAnotherAttempt()
    {
        var existing = Existing(ProgressStatus.GaveUp);
        existing.Attempts = 1;
        SetupExisting(existing);

        var result = await _sut.SaveAsync(_userId, SongId,
            new SaveProgressRequest { FoundKeys = ["hey"], ElapsedSeconds = 40, Status = ProgressStatusDto.GaveUp });

        Assert.Equal(1, result.Attempts);
    }

    [Fact]
    public async Task SaveAsync_ClientClaimsCompletedWithoutAllWords_StaysInProgress()
    {
        SetupExisting(null);

        var result = await _sut.SaveAsync(_userId, SongId,
            new SaveProgressRequest { FoundKeys = ["hey"], ElapsedSeconds = 5, Status = ProgressStatusDto.Completed });

        Assert.Equal(ProgressStatusDto.InProgress, result.Status);
        Assert.Null(result.BestTimeSeconds);
    }

    [Fact]
    public async Task SaveAsync_PlayAgain_ResetsCurrentAttemptButKeepsBest()
    {
        SetupExisting(Existing(ProgressStatus.Completed, bestPercentage: 100, bestTime: 25));

        var result = await _sut.SaveAsync(_userId, SongId, new SaveProgressRequest { FoundKeys = [], ElapsedSeconds = 0 });

        Assert.Equal(0, result.Percentage);
        Assert.Equal(ProgressStatusDto.InProgress, result.Status);
        Assert.Equal(100, result.BestPercentage);
        Assert.Equal(25, result.BestTimeSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24 * 60 * 60 + 1)]
    public async Task SaveAsync_InvalidElapsedTime_ThrowsArgumentException(int seconds)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SaveAsync(_userId, SongId, new SaveProgressRequest { ElapsedSeconds = seconds }));
    }

    [Fact]
    public async Task GetMyProgressAsync_NeverPlayed_ReturnsNull()
    {
        SetupExisting(null);

        var result = await _sut.GetMyProgressAsync(_userId, SongId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSongPlayersAsync_OrdersByBestScoreThenFastestTime()
    {
        var slow = Existing(ProgressStatus.Completed, 100, 90); slow.User = new User { Username = "slow", NormalizedUsername = "SLOW", HashedPassword = "x" };
        var fast = Existing(ProgressStatus.Completed, 100, 30); fast.User = new User { Username = "fast", NormalizedUsername = "FAST", HashedPassword = "x" };
        var half = Existing(ProgressStatus.InProgress, 50); half.User = new User { Username = "half", NormalizedUsername = "HALF", HashedPassword = "x" };
        _progressRepository.Setup(r => r.GetBySongAsync(SongId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([half, slow, fast]);

        var result = await _sut.GetSongPlayersAsync(SongId);

        Assert.Equal(new[] { "fast", "slow", "half" }, result.Select(p => p.Username));
    }
}
