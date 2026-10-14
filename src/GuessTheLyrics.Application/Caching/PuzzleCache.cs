using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Caching;

public interface IPuzzleCache
{
    bool TryGet(long songId, out LyricsPuzzleResponse puzzle);

    /// <summary>Also returns an expired entry: used as a fallback while the lyrics service is down.</summary>
    bool TryGetStale(long songId, out LyricsPuzzleResponse puzzle);

    void Set(long songId, LyricsPuzzleResponse puzzle);
}

/// <summary>
/// In-memory cache of tokenised lyrics. Saving progress needs the puzzle to validate the found words,
/// so without this every save would call the lyrics service again. Expired entries stay until the cache is full,
/// so a song that was played before keeps working while the lyrics service is down. Single-instance only.
/// </summary>
public sealed class PuzzleCache : IPuzzleCache
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(30);
    private const int MaxEntries = 500;

    private readonly Dictionary<long, (LyricsPuzzleResponse Puzzle, DateTime ExpiresAt)> _entries = [];
    private readonly object _lock = new();

    public bool TryGet(long songId, out LyricsPuzzleResponse puzzle)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(songId, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                puzzle = entry.Puzzle;
                return true;
            }

            puzzle = null!;
            return false;
        }
    }

    public bool TryGetStale(long songId, out LyricsPuzzleResponse puzzle)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(songId, out var entry))
            {
                puzzle = entry.Puzzle;
                return true;
            }

            puzzle = null!;
            return false;
        }
    }

    public void Set(long songId, LyricsPuzzleResponse puzzle)
    {
        lock (_lock)
        {
            if (_entries.Count >= MaxEntries)
                RemoveExpiredOrOldest();

            _entries[songId] = (puzzle, DateTime.UtcNow.Add(TimeToLive));
        }
    }

    private void RemoveExpiredOrOldest()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _entries.Where(e => e.Value.ExpiresAt <= now).Select(e => e.Key).ToList())
            _entries.Remove(key);

        if (_entries.Count >= MaxEntries)
            _entries.Remove(_entries.MinBy(e => e.Value.ExpiresAt).Key);
    }
}
