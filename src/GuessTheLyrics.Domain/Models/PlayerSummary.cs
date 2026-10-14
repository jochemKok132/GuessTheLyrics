namespace GuessTheLyrics.Domain.Models;

/// <summary>Aggregated statistics of one player, used for the leaderboard and profiles.</summary>
public sealed record PlayerSummary(
    Guid UserId,
    string Username,
    DateTime JoinedAt,
    int SongsPlayed,
    int SongsCompleted,
    int WordsFound,
    double AverageBestPercentage,
    DateTime? LastPlayedAt);
