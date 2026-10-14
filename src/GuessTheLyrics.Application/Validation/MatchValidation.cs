using GuessTheLyrics.Shared.Matches;

namespace GuessTheLyrics.Application.Validation;

public static class MatchValidation
{
    public static void Validate(this MatchProgressRequest request)
    {
        if (request.FoundKeys is null)
            throw new ArgumentException("Found words are missing.");

        if (request.FoundKeys.Count > ProgressValidation.MaxFoundKeys)
            throw new ArgumentException("Too many found words.");
    }
}
