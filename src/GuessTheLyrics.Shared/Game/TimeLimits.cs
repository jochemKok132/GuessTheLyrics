namespace GuessTheLyrics.Shared.Game;

/// <summary>
/// How long a player gets for a song: a fixed amount per different word, so a short song with a few repeated
/// words is quick and a long, varied song gets more time. Used by the API (validation, matches) and the UI (countdown).
/// </summary>
public static class TimeLimits
{
    public const int SecondsPerUniqueWord = 3;
    public const int MinimumSeconds = 60;
    public const int MaximumSeconds = 20 * 60;

    /// <summary>Extra seconds the server accepts after the limit, for network delay.</summary>
    public const int GraceSeconds = 5;

    /// <summary>3 seconds per different word, between 1 and 20 minutes, rounded up to a multiple of 15 seconds.</summary>
    public static int ForUniqueWords(int uniqueWords)
    {
        var seconds = Math.Clamp(uniqueWords * SecondsPerUniqueWord, MinimumSeconds, MaximumSeconds);
        return (int)Math.Ceiling(seconds / 15.0) * 15;
    }
}
