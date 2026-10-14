using System.Collections.Concurrent;
using System.Security.Cryptography;
using GuessTheLyrics.Domain.Exceptions;

namespace GuessTheLyrics.Application.Matches;

/// <summary>
/// All live matches, in memory. Matches are short-lived, so they are not stored in the database: an API restart
/// (e.g. an automatic update) ends running matches. Single-instance only.
/// </summary>
public sealed class MatchStore
{
    public const int MaxMatches = 500;

    // No 0/O, 1/I/L: codes are read aloud and typed over.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 6;

    private readonly ConcurrentDictionary<string, Match> _matches = new(StringComparer.Ordinal);

    public Match? Get(string code) => _matches.TryGetValue(code, out var match) ? match : null;

    public IReadOnlyList<Match> All() => _matches.Values.ToList();

    /// <summary>Adds a match under a new, unused code built by <paramref name="create"/>.</summary>
    public Match Add(Func<string, Match> create)
    {
        if (_matches.Count >= MaxMatches)
            throw new ConflictException("There are too many matches right now. Try again in a few minutes.");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var match = create(NewCode());
            if (_matches.TryAdd(match.Code, match))
                return match;
        }

        throw new InvalidOperationException("Could not find a free match code.");
    }

    public void Remove(string code) => _matches.TryRemove(code, out _);

    /// <summary>Removes abandoned lobbies and old finished matches. Returns how many were removed.</summary>
    public int RemoveExpired(DateTime now)
    {
        var removed = 0;
        foreach (var match in _matches.Values)
        {
            bool expired;
            lock (match.Lock)
            {
                match.Refresh(now);
                expired = match.Status switch
                {
                    MatchStatus.Lobby => now - match.CreatedAt > TimeSpan.FromHours(1),
                    MatchStatus.Finished => now - (match.FinishedAt ?? now) > TimeSpan.FromMinutes(30),
                    _ => false
                };
            }

            if (expired && _matches.TryRemove(match.Code, out _))
                removed++;
        }
        return removed;
    }

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValidCode(string code) =>
        code.Length == CodeLength && code.All(c => CodeAlphabet.Contains(c));

    private static string NewCode() =>
        string.Create(CodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        });
}
