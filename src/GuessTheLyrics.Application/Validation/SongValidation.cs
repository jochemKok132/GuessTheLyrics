using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Validation;

public static class SongValidation
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 100;

    public static void Validate(this SongSearchRequest request)
    {
        var query = request.Query?.Trim() ?? string.Empty;

        if (query.Length < MinQueryLength)
            throw new ArgumentException($"Type at least {MinQueryLength} characters to search.");

        if (query.Length > MaxQueryLength)
            throw new ArgumentException($"A search can be at most {MaxQueryLength} characters.");
    }

    public static void ValidateSongId(long id)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid song id.");
    }
}
