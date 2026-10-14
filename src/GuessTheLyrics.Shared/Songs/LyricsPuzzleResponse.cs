namespace GuessTheLyrics.Shared.Songs;

public sealed class LyricsPuzzleResponse
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }

    /// <summary>Number of guessable words in the song, repeats included.</summary>
    public int TotalWords { get; init; }

    /// <summary>Number of different words the player has to type.</summary>
    public int UniqueWords { get; init; }

    /// <summary>Countdown for this song in seconds, see <see cref="Game.TimeLimits"/>.</summary>
    public int TimeLimitSeconds { get; init; }

    public IReadOnlyList<LyricsLineResponse> Lines { get; init; } = [];
}

public sealed class LyricsLineResponse
{
    /// <summary>True for section labels such as "[Chorus]"; these are shown, not guessed.</summary>
    public bool IsHeader { get; init; }

    /// <summary>Empty for a blank line between verses.</summary>
    public IReadOnlyList<LyricsTokenResponse> Tokens { get; init; } = [];
}

public sealed class LyricsTokenResponse
{
    /// <summary>The text exactly as it appears in the lyrics.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Normalised word the player has to type (see <see cref="WordNormalizer"/>).
    /// <c>null</c> for spaces and punctuation, which are always visible.
    /// </summary>
    public string? Key { get; init; }
}
