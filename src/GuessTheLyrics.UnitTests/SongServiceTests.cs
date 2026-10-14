using GuessTheLyrics.Application.Caching;
using GuessTheLyrics.Application.Services;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;
using GuessTheLyrics.Shared.Songs;
using Moq;

namespace GuessTheLyrics.UnitTests;

public class SongServiceTests
{
    private readonly Mock<ILyricsProvider> _lyricsProvider = new();
    private readonly SongService _sut;

    public SongServiceTests()
    {
        _sut = new SongService(_lyricsProvider.Object, new PuzzleCache());
    }

    private static LyricsTrack Track(long id, string title = "Song", string artist = "Artist",
        string? lyrics = "hello world", bool instrumental = false) => new()
    {
        Id = id,
        TrackName = title,
        ArtistName = artist,
        Duration = 185,
        Instrumental = instrumental,
        PlainLyrics = lyrics
    };

    [Theory]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task SearchAsync_QueryTooShort_ThrowsArgumentException(string query)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(new SongSearchRequest { Query = query }));

        _lyricsProvider.Verify(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_QueryTooLong_ThrowsArgumentException()
    {
        var request = new SongSearchRequest { Query = new string('a', 101) };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(request));
    }

    [Fact]
    public async Task SearchAsync_ValidQuery_PassesTrimmedQueryToProvider()
    {
        string? captured = null;
        _lyricsProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync([]);

        await _sut.SearchAsync(new SongSearchRequest { Query = "  the bad touch  " });

        Assert.Equal("the bad touch", captured);
    }

    [Fact]
    public async Task SearchAsync_TracksWithoutLyrics_AreFilteredOut()
    {
        _lyricsProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Track(1, "With lyrics"),
                Track(2, "Instrumental", instrumental: true),
                Track(3, "No lyrics", lyrics: null),
                Track(4, "Blank lyrics", lyrics: "   ")
            ]);

        var results = await _sut.SearchAsync(new SongSearchRequest { Query = "song" });

        var result = Assert.Single(results);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task SearchAsync_DuplicateArtistAndTitle_KeepsFirstOnly()
    {
        _lyricsProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Track(1, "The Bad Touch", "Bloodhound Gang"),
                Track(2, "the bad touch", "BLOODHOUND GANG"),
                Track(3, "The Bad Touch (Live)", "Bloodhound Gang")
            ]);

        var results = await _sut.SearchAsync(new SongSearchRequest { Query = "bad touch" });

        Assert.Equal(new long[] { 1, 3 }, results.Select(r => r.Id));
    }

    [Fact]
    public async Task SearchAsync_ValidTrack_MapsWordCountAndDuration()
    {
        _lyricsProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Track(1, lyrics: "one two\nthree, three")]);

        var result = Assert.Single(await _sut.SearchAsync(new SongSearchRequest { Query = "song" }));

        Assert.Equal(4, result.WordCount);
        Assert.Equal(185, result.DurationSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetPuzzleAsync_InvalidId_ThrowsArgumentException(long id)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPuzzleAsync(id));
    }

    [Fact]
    public async Task GetPuzzleAsync_SongNotFound_ThrowsKeyNotFoundException()
    {
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LyricsTrack?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetPuzzleAsync(42));
    }

    [Fact]
    public async Task GetPuzzleAsync_InstrumentalSong_ThrowsKeyNotFoundException()
    {
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Track(42, instrumental: true));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetPuzzleAsync(42));
    }

    [Fact]
    public async Task GetPuzzleAsync_SongFound_ReturnsTokenizedPuzzle()
    {
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Track(42, "Title", "Artist", "Na na na\n\nhey jude"));

        var puzzle = await _sut.GetPuzzleAsync(42);

        Assert.Equal(42, puzzle.Id);
        Assert.Equal("Title", puzzle.Title);
        Assert.Equal(5, puzzle.TotalWords);
        Assert.Equal(3, puzzle.UniqueWords);
        Assert.Equal(3, puzzle.Lines.Count);
    }

    [Fact]
    public async Task GetPuzzleAsync_CalledTwice_UsesCacheForSecondCall()
    {
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Track(42));

        await _sut.GetPuzzleAsync(42);
        await _sut.GetPuzzleAsync(42);

        _lyricsProvider.Verify(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPuzzleAsync_LyricsServiceDownButOlderCopyCached_ReturnsCachedCopy()
    {
        var stale = new LyricsPuzzleResponse { Id = 42, Title = "Cached" };
        var cache = new Mock<IPuzzleCache>();
        cache.Setup(c => c.TryGet(42, out It.Ref<LyricsPuzzleResponse>.IsAny)).Returns(false);
        cache.Setup(c => c.TryGetStale(42, out stale)).Returns(true);
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LyricsServiceException(LyricsServiceFailure.Unavailable, "down"));
        var sut = new SongService(_lyricsProvider.Object, cache.Object);

        var puzzle = await sut.GetPuzzleAsync(42);

        Assert.Equal("Cached", puzzle.Title);
    }

    [Fact]
    public async Task GetPuzzleAsync_LyricsServiceDownAndNothingCached_ThrowsLyricsServiceException()
    {
        _lyricsProvider.Setup(p => p.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LyricsServiceException(LyricsServiceFailure.Unavailable, "down"));

        await Assert.ThrowsAsync<LyricsServiceException>(() => _sut.GetPuzzleAsync(42));
    }

    [Fact]
    public async Task SearchAsync_LyricsServiceDown_LetsLyricsServiceExceptionThrough()
    {
        _lyricsProvider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LyricsServiceException(LyricsServiceFailure.Timeout, "slow"));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() =>
            _sut.SearchAsync(new SongSearchRequest { Query = "the bad touch" }));

        Assert.Equal(LyricsServiceFailure.Timeout, exception.Failure);
    }
}
