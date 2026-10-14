using System.Globalization;
using System.Text;

namespace GuessTheLyrics.Shared;

/// <summary>
/// Turns a word into the key used to compare a guess with the lyrics.
/// Lives in Shared because the API (building the puzzle) and the UI (checking what
/// the player types) must normalise in exactly the same way.
/// </summary>
public static class WordNormalizer
{
    /// <summary>
    /// Lowercases, strips accents and drops everything that is not a letter or digit.
    /// "Don't" → "dont", "Café" → "cafe", "HEY!" → "hey".
    /// </summary>
    public static string Normalize(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        var decomposed = word.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
