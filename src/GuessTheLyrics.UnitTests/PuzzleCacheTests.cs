using GuessTheLyrics.Application.Caching;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.UnitTests;

public class PuzzleCacheTests
{
    private readonly PuzzleCache _sut = new();

    [Fact]
    public void TryGet_UnknownSong_ReturnsFalse()
    {
        Assert.False(_sut.TryGet(1, out _));
        Assert.False(_sut.TryGetStale(1, out _));
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsPuzzle()
    {
        var puzzle = new LyricsPuzzleResponse { Id = 1, Title = "Song" };

        _sut.Set(1, puzzle);

        Assert.True(_sut.TryGet(1, out var fresh));
        Assert.Same(puzzle, fresh);
        Assert.True(_sut.TryGetStale(1, out var stale));
        Assert.Same(puzzle, stale);
    }
}
