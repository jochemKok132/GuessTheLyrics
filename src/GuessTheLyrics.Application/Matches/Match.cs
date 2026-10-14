namespace GuessTheLyrics.Application.Matches;

public enum MatchStatus
{
    Lobby,
    Playing,
    Finished
}

/// <summary>
/// A live multiplayer match, kept in memory (see <see cref="MatchStore"/>). Every read and write happens inside
/// <c>lock (match.Lock)</c>, because several players poll and send progress at the same time.
/// </summary>
public sealed class Match
{
    public required string Code { get; init; }
    public required long SongId { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public required int TotalWords { get; init; }
    public required int UniqueWords { get; init; }
    public required int TimeLimitSeconds { get; init; }

    /// <summary>Normalised word → occurrences, to validate and count what players send.</summary>
    public required IReadOnlyDictionary<string, int> WordCounts { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required Guid HostId { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Lobby;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public List<MatchPlayer> Players { get; } = [];

    public object Lock { get; } = new();

    public MatchPlayer? FindPlayer(Guid userId) => Players.FirstOrDefault(p => p.UserId == userId);

    public IEnumerable<MatchPlayer> ActivePlayers => Players.Where(p => !p.Left);

    /// <summary>Ends the match when the time is up, or when every remaining player found all words (or left).</summary>
    public void Refresh(DateTime now)
    {
        if (Status != MatchStatus.Playing || EndsAt is null)
            return;

        var everyoneDone = ActivePlayers.All(p => p.FinishedAt is not null);
        if (now >= EndsAt || everyoneDone)
        {
            Status = MatchStatus.Finished;
            FinishedAt = now < EndsAt ? now : EndsAt;
        }
    }

    /// <summary>
    /// Finished players first (fastest first), then most words found, then whoever reached that count first.
    /// Players who left are last.
    /// </summary>
    public List<MatchPlayer> Ranking() =>
        Players
            .OrderBy(p => p.Left)
            .ThenBy(p => p.FinishedAt ?? DateTime.MaxValue)
            .ThenByDescending(p => p.FoundWordCount)
            .ThenBy(p => p.LastFoundAt ?? DateTime.MaxValue)
            .ThenBy(p => p.JoinedAt)
            .ToList();
}

public sealed class MatchPlayer
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required DateTime JoinedAt { get; init; }

    public HashSet<string> FoundKeys { get; } = [];
    public int FoundWordCount { get; set; }
    public DateTime? LastFoundAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public bool Left { get; set; }
}
