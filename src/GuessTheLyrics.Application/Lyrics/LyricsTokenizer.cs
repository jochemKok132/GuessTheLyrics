using System.Text.RegularExpressions;
using GuessTheLyrics.Shared;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Lyrics;

/// <summary>
/// Splits plain-text lyrics into lines of word tokens (to guess) and separator tokens
/// (spaces and punctuation, always visible). Pure and side-effect free.
/// </summary>
public static partial class LyricsTokenizer
{
    // A word is a run of letters/digits, optionally joined by apostrophes: "don't", "rock'n'roll".
    // Hyphens split words, so "ever-lasting" is two words.
    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+(?:['’][\p{L}\p{M}\p{N}]+)*")]
    private static partial Regex WordRegex();

    public static IReadOnlyList<LyricsLineResponse> Tokenize(string? lyrics)
    {
        var result = new List<LyricsLineResponse>();
        if (string.IsNullOrWhiteSpace(lyrics))
            return result;

        var rawLines = lyrics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var rawLine in rawLines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                // Collapse runs of blank lines into one verse break, and never start with one.
                if (result.Count > 0 && result[^1].Tokens.Count > 0)
                    result.Add(new LyricsLineResponse());
                continue;
            }

            if (IsHeader(line))
            {
                result.Add(new LyricsLineResponse
                {
                    IsHeader = true,
                    Tokens = [new LyricsTokenResponse { Text = line }]
                });
                continue;
            }

            result.Add(new LyricsLineResponse { Tokens = TokenizeLine(line) });
        }

        // Drop a trailing verse break.
        if (result.Count > 0 && result[^1].Tokens.Count == 0)
            result.RemoveAt(result.Count - 1);

        return result;
    }

    private static bool IsHeader(string line) =>
        line.Length > 2 && line[0] == '[' && line[^1] == ']';

    private static List<LyricsTokenResponse> TokenizeLine(string line)
    {
        var tokens = new List<LyricsTokenResponse>();
        var position = 0;

        foreach (Match match in WordRegex().Matches(line))
        {
            if (match.Index > position)
                tokens.Add(new LyricsTokenResponse { Text = line[position..match.Index] });

            var key = WordNormalizer.Normalize(match.Value);
            tokens.Add(new LyricsTokenResponse
            {
                Text = match.Value,
                Key = key.Length > 0 ? key : null
            });

            position = match.Index + match.Length;
        }

        if (position < line.Length)
            tokens.Add(new LyricsTokenResponse { Text = line[position..] });

        return tokens;
    }

    public static int CountWords(IEnumerable<LyricsLineResponse> lines) =>
        lines.Sum(l => l.Tokens.Count(t => t.Key is not null));

    public static int CountUniqueWords(IEnumerable<LyricsLineResponse> lines) =>
        lines.SelectMany(l => l.Tokens)
             .Where(t => t.Key is not null)
             .Select(t => t.Key!)
             .Distinct()
             .Count();
}
