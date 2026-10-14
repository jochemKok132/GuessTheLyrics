using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Validation;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Shared.Matches;

namespace GuessTheLyrics.Application.Matches;

/// <summary>
/// Multiplayer: a host creates a match for a song, others join with the code, the host starts it, and after a short
/// countdown everyone races to type the same lyrics before the time runs out. The server is the referee: it owns the
/// clock, validates every word against the real lyrics and decides the ranking.
/// </summary>
public sealed class MatchService(MatchStore store, ISongService songService, TimeProvider timeProvider) : IMatchService
{
    public const int MaxPlayers = 8;
    public const int CountdownSeconds = 5;
    public const int MaxOpenMatchesListed = 20;

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<MatchResponse> CreateAsync(Guid userId, string username, CreateMatchRequest request, CancellationToken cancellationToken = default)
    {
        SongValidation.ValidateSongId(request.SongId);

        // Validates that the song exists and has lyrics, and gives us the words to check answers against.
        var puzzle = await songService.GetPuzzleAsync(request.SongId, cancellationToken);
        var now = Now;

        var wordCounts = puzzle.Lines
            .SelectMany(l => l.Tokens)
            .Where(t => t.Key is not null)
            .GroupBy(t => t.Key!)
            .ToDictionary(g => g.Key, g => g.Count());

        var match = store.Add(code => new Match
        {
            Code = code,
            SongId = puzzle.Id,
            Title = puzzle.Title,
            Artist = puzzle.Artist,
            Album = puzzle.Album,
            TotalWords = puzzle.TotalWords,
            UniqueWords = puzzle.UniqueWords,
            TimeLimitSeconds = puzzle.TimeLimitSeconds,
            WordCounts = wordCounts,
            CreatedAt = now,
            HostId = userId
        });

        lock (match.Lock)
        {
            match.Players.Add(new MatchPlayer { UserId = userId, Username = username, JoinedAt = now });
            return match.ToResponse(userId, now);
        }
    }

    public MatchResponse Get(string code, Guid userId)
    {
        var match = Find(code);
        lock (match.Lock)
        {
            var now = Now;
            match.Refresh(now);
            return match.ToResponse(userId, now);
        }
    }

    public IReadOnlyList<MatchSummaryResponse> GetOpenMatches()
    {
        var now = Now;
        var open = new List<MatchSummaryResponse>();

        foreach (var match in store.All())
        {
            lock (match.Lock)
            {
                if (match.Status == MatchStatus.Lobby && match.Players.Count < MaxPlayers)
                    open.Add(match.ToSummary());
            }
        }

        return open.OrderByDescending(m => m.CreatedAt).Take(MaxOpenMatchesListed).ToList();
    }

    public MatchResponse Join(string code, Guid userId, string username)
    {
        var match = Find(code);
        lock (match.Lock)
        {
            var now = Now;
            match.Refresh(now);

            if (match.FindPlayer(userId) is { Left: false })
                return match.ToResponse(userId, now); // already in: joining twice is fine

            if (match.Status != MatchStatus.Lobby)
                throw new ConflictException("This match has already started. Ask for a new code, or watch this one.");

            if (match.Players.Count >= MaxPlayers)
                throw new ConflictException($"This match is full ({MaxPlayers} players).");

            match.Players.RemoveAll(p => p.UserId == userId); // someone who left the lobby comes back
            match.Players.Add(new MatchPlayer { UserId = userId, Username = username, JoinedAt = now });
            return match.ToResponse(userId, now);
        }
    }

    public MatchResponse Leave(string code, Guid userId)
    {
        var match = Find(code);
        lock (match.Lock)
        {
            var now = Now;
            match.Refresh(now);

            var player = match.FindPlayer(userId)
                         ?? throw new KeyNotFoundException("You are not in this match.");

            if (match.Status == MatchStatus.Lobby)
            {
                match.Players.Remove(player);

                if (match.Players.Count == 0)
                {
                    store.Remove(match.Code);
                }
                else if (match.HostId == userId)
                {
                    match.HostId = match.Players[0].UserId; // the longest-waiting player becomes host
                }
            }
            else if (match.Status == MatchStatus.Playing)
            {
                // Keep the score on the board, but stop waiting for this player.
                player.Left = true;
                match.Refresh(now);
            }

            return match.ToResponse(userId, now);
        }
    }

    public MatchResponse Start(string code, Guid userId)
    {
        var match = Find(code);
        lock (match.Lock)
        {
            var now = Now;
            match.Refresh(now);

            if (match.HostId != userId)
                throw new UnauthorizedAccessException("Only the host can start the match.");

            if (match.Status != MatchStatus.Lobby)
                throw new ConflictException("This match has already started.");

            match.Status = MatchStatus.Playing;
            match.StartsAt = now.AddSeconds(CountdownSeconds);
            match.EndsAt = match.StartsAt.Value.AddSeconds(match.TimeLimitSeconds);
            return match.ToResponse(userId, now);
        }
    }

    public MatchResponse SubmitProgress(string code, Guid userId, MatchProgressRequest request)
    {
        request.Validate();

        var match = Find(code);
        lock (match.Lock)
        {
            var now = Now;
            match.Refresh(now);

            var player = match.FindPlayer(userId)
                         ?? throw new KeyNotFoundException("You are not in this match.");

            if (match.Status == MatchStatus.Lobby || now < match.StartsAt)
                throw new ArgumentException("The match hasn't started yet.");

            // Late, finished or left: nothing changes, but answer with the current board (no error for the player).
            var accepting = match.Status == MatchStatus.Playing || now <= match.EndsAt!.Value.AddSeconds(Shared.Game.TimeLimits.GraceSeconds);
            if (!accepting || player.Left || player.FinishedAt is not null)
                return match.ToResponse(userId, now);

            // Words only get added, never removed, and only real words of this song count.
            var before = player.FoundWordCount;
            foreach (var key in request.FoundKeys.Where(k => k is not null && match.WordCounts.ContainsKey(k)))
            {
                if (player.FoundKeys.Add(key))
                    player.FoundWordCount += match.WordCounts[key];
            }

            if (player.FoundWordCount > before)
                player.LastFoundAt = now;

            if (player.FoundKeys.Count == match.WordCounts.Count)
                player.FinishedAt = now < match.EndsAt ? now : match.EndsAt;

            match.Refresh(now);
            return match.ToResponse(userId, now);
        }
    }

    private Match Find(string code)
    {
        var normalized = MatchStore.NormalizeCode(code);
        if (!MatchStore.IsValidCode(normalized))
            throw new ArgumentException($"A match code has {MatchStore.CodeLength} letters and digits.");

        return store.Get(normalized)
               ?? throw new KeyNotFoundException("Match not found. Check the code; matches end an hour after they were created.");
    }
}
