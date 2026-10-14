using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Enums;
using GuessTheLyrics.Domain.Models;
using GuessTheLyrics.Shared.Players;
using GuessTheLyrics.Shared.Progress;

namespace GuessTheLyrics.Application.Mapping;

public static class ProgressMappings
{
    public static SongProgressResponse ToResponse(this SongProgress progress, bool includeFoundKeys) => new()
    {
        SongId = progress.SongId,
        Title = progress.Title,
        Artist = progress.Artist,
        Album = progress.Album,
        TotalWords = progress.TotalWords,
        UniqueWords = progress.UniqueWords,
        FoundWordCount = progress.FoundWordCount,
        Percentage = progress.Percentage,
        ElapsedSeconds = progress.ElapsedSeconds,
        Status = progress.Status.ToDto(),
        Attempts = progress.Attempts,
        BestPercentage = progress.BestPercentage,
        BestTimeSeconds = progress.BestTimeSeconds,
        LastPlayedAt = progress.UpdatedAt ?? progress.CreatedAt,
        FoundKeys = includeFoundKeys
            ? progress.FoundKeys.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
            : null
    };

    public static SongPlayerResponse ToSongPlayerResponse(this SongProgress progress) => new()
    {
        Username = progress.User?.Username ?? "Unknown player",
        Percentage = progress.Percentage,
        Status = progress.Status.ToDto(),
        BestPercentage = progress.BestPercentage,
        BestTimeSeconds = progress.BestTimeSeconds,
        Attempts = progress.Attempts,
        LastPlayedAt = progress.UpdatedAt ?? progress.CreatedAt
    };

    public static PlayerSummaryResponse ToResponse(this PlayerSummary summary, int rank) => new()
    {
        Rank = rank,
        Username = summary.Username,
        JoinedAt = summary.JoinedAt,
        SongsPlayed = summary.SongsPlayed,
        SongsCompleted = summary.SongsCompleted,
        WordsFound = summary.WordsFound,
        AverageBestPercentage = (int)Math.Round(summary.AverageBestPercentage),
        LastPlayedAt = summary.LastPlayedAt
    };

    public static ProgressStatusDto ToDto(this ProgressStatus status) => status switch
    {
        ProgressStatus.InProgress => ProgressStatusDto.InProgress,
        ProgressStatus.Completed => ProgressStatusDto.Completed,
        ProgressStatus.GaveUp => ProgressStatusDto.GaveUp,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown progress status.")
    };

    public static ProgressStatus ToDomain(this ProgressStatusDto status) => status switch
    {
        ProgressStatusDto.InProgress => ProgressStatus.InProgress,
        ProgressStatusDto.Completed => ProgressStatus.Completed,
        ProgressStatusDto.GaveUp => ProgressStatus.GaveUp,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown progress status.")
    };
}
