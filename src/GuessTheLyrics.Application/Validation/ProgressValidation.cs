using GuessTheLyrics.Shared.Progress;

namespace GuessTheLyrics.Application.Validation;

public static class ProgressValidation
{
    public const int MaxElapsedSeconds = 24 * 60 * 60;
    public const int MaxFoundKeys = 5000;

    public static void Validate(this SaveProgressRequest request)
    {
        if (request.FoundKeys is null)
            throw new ArgumentException("Found words are missing.");

        if (request.FoundKeys.Count > MaxFoundKeys)
            throw new ArgumentException("Too many found words.");

        if (request.ElapsedSeconds < 0 || request.ElapsedSeconds > MaxElapsedSeconds)
            throw new ArgumentException("Invalid play time.");

        if (!Enum.IsDefined(request.Status))
            throw new ArgumentException("Invalid status.");
    }
}
