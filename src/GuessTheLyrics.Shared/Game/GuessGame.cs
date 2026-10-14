using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Shared.Game;

/// <summary>Lines between two blank lines, with an optional "[Chorus]"-style label.</summary>
public sealed record LyricsVerse(string Label, List<LyricsLineResponse> Lines, HashSet<string> Keys);

/// <summary>Result of processing what the player typed.</summary>
/// <param name="Input">What the input box should contain now (cleared after an accepted or submitted word).</param>
/// <param name="Accepted">A new word was found.</param>
/// <param name="Typed">The player typed something that counts (starts the clock).</param>
public readonly record struct GuessResult(string Input, bool Accepted, bool Typed);

/// <summary>
/// The word-guessing rules, shared by solo play and multiplayer. Pure logic without UI or timers, so it is unit tested.
/// </summary>
public sealed class GuessGame
{
    private readonly Dictionary<string, int> _wordCounts;
    private readonly HashSet<string> _found = [];

    public GuessGame(LyricsPuzzleResponse puzzle)
    {
        Puzzle = puzzle;
        _wordCounts = puzzle.Lines
            .SelectMany(l => l.Tokens)
            .Where(t => t.Key is not null)
            .GroupBy(t => t.Key!)
            .ToDictionary(g => g.Key, g => g.Count());
        Verses = BuildVerses(puzzle.Lines);
    }

    public LyricsPuzzleResponse Puzzle { get; }
    public IReadOnlyList<LyricsVerse> Verses { get; }

    /// <summary>Normalised word → how many times it appears in the song.</summary>
    public IReadOnlyDictionary<string, int> WordCounts => _wordCounts;

    public IReadOnlySet<string> Found => _found;
    public int FoundWordCount { get; private set; }
    public string? LastFound { get; private set; }

    /// <summary>Message about the last submitted wrong word, e.g. "zzz" is not in the song.</summary>
    public string? Feedback { get; private set; }

    public bool AllFound => _wordCounts.Count > 0 && _found.Count == _wordCounts.Count;

    public int Percentage => Puzzle.TotalWords == 0 ? 0 : (int)Math.Floor(FoundWordCount * 100.0 / Puzzle.TotalWords);

    /// <summary>
    /// Handles the current input. A word is accepted as soon as it is unambiguous; when a longer hidden word starts
    /// the same way ("a" while "again" is hidden) the player keeps typing, or ends with a space/Enter to submit.
    /// A submitted wrong word clears the input and sets <see cref="Feedback"/>.
    /// </summary>
    public GuessResult Guess(string input)
    {
        var submitted = input.Length > 0 && char.IsWhiteSpace(input[^1]);
        var key = WordNormalizer.Normalize(input);

        if (key.Length == 0)
            return new GuessResult(submitted ? string.Empty : input, Accepted: false, Typed: false);

        var isUnfoundWord = _wordCounts.ContainsKey(key) && !_found.Contains(key);

        if (isUnfoundWord && (submitted || !LongerUnfoundWordStartsWith(key)))
        {
            Accept(key);
            return new GuessResult(string.Empty, Accepted: true, Typed: true);
        }

        if (submitted)
        {
            Feedback = _found.Contains(key) ? $"\"{key}\" is already found" : $"\"{key}\" is not in the song";
            return new GuessResult(string.Empty, Accepted: false, Typed: true);
        }

        return new GuessResult(input, Accepted: false, Typed: true);
    }

    /// <summary>Marks saved words as found (resuming a game). Unknown words are ignored.</summary>
    public void Restore(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (_wordCounts.TryGetValue(key, out var count) && _found.Add(key))
                FoundWordCount += count;
        }
    }

    public void Reset()
    {
        _found.Clear();
        FoundWordCount = 0;
        LastFound = null;
        Feedback = null;
    }

    private void Accept(string key)
    {
        _found.Add(key);
        FoundWordCount += _wordCounts[key];
        LastFound = key;
        Feedback = null;
    }

    private bool LongerUnfoundWordStartsWith(string key) =>
        _wordCounts.Keys.Any(k => k.Length > key.Length && !_found.Contains(k) && k.StartsWith(key, StringComparison.Ordinal));

    private static List<LyricsVerse> BuildVerses(IReadOnlyList<LyricsLineResponse> lines)
    {
        var result = new List<LyricsVerse>();
        LyricsVerse? current = null;

        foreach (var line in lines)
        {
            if (line.Tokens.Count == 0)
            {
                current = null; // blank line: the next line starts a new verse
                continue;
            }

            if (line.IsHeader)
            {
                current = new LyricsVerse(line.Tokens[0].Text.Trim('[', ']', ' '), [], []);
                result.Add(current);
                continue;
            }

            if (current is null)
            {
                current = new LyricsVerse(string.Empty, [], []);
                result.Add(current);
            }

            current.Lines.Add(line);
            foreach (var token in line.Tokens.Where(t => t.Key is not null))
                current.Keys.Add(token.Key!);
        }

        // A label followed by a blank line gives an empty verse; drop those.
        return result.Where(v => v.Lines.Count > 0).ToList();
    }
}
