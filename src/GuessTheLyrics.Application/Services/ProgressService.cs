using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Mapping;
using GuessTheLyrics.Application.Validation;
using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Enums;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Shared.Progress;
using GuessTheLyrics.Shared.Songs;

namespace GuessTheLyrics.Application.Services;

public sealed class ProgressService(ISongProgressRepository progressRepository, ISongService songService) : IProgressService
{
    public async Task<SongProgressResponse?> GetMyProgressAsync(Guid userId, long songId, CancellationToken cancellationToken = default)
    {
        SongValidation.ValidateSongId(songId);

        var progress = await progressRepository.GetAsync(userId, songId, cancellationToken);
        return progress?.ToResponse(includeFoundKeys: true);
    }

    public async Task<SongProgressResponse> SaveAsync(Guid userId, long songId, SaveProgressRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();

        // The server recounts everything from the real lyrics, so the client can only claim words that exist.
        var puzzle = await songService.GetPuzzleAsync(songId, cancellationToken);
        var wordCounts = CountWords(puzzle);

        var foundKeys = request.FoundKeys
            .Where(k => k is not null && wordCounts.ContainsKey(k))
            .Distinct()
            .ToList();
        var foundWordCount = foundKeys.Sum(k => wordCounts[k]);

        var status = foundKeys.Count == wordCounts.Count && wordCounts.Count > 0
            ? ProgressStatus.Completed
            : request.Status.ToDomain() == ProgressStatus.GaveUp ? ProgressStatus.GaveUp : ProgressStatus.InProgress;

        var progress = await progressRepository.GetAsync(userId, songId, cancellationToken);
        var isNew = progress is null;

        progress ??= new SongProgress
        {
            UserId = userId,
            SongId = songId,
            Title = puzzle.Title,
            Artist = puzzle.Artist,
            Album = puzzle.Album
        };

        // Only an attempt that was still running can finish; saving a finished game twice changes nothing.
        var finishesAttempt = status != ProgressStatus.InProgress && progress.Status == ProgressStatus.InProgress;

        progress.TotalWords = puzzle.TotalWords;
        progress.UniqueWords = puzzle.UniqueWords;
        progress.FoundKeys = string.Join(' ', foundKeys);
        progress.FoundWordCount = foundWordCount;
        progress.ElapsedSeconds = request.ElapsedSeconds;
        progress.Status = status;
        progress.BestPercentage = Math.Max(progress.BestPercentage, progress.Percentage);

        if (finishesAttempt)
        {
            progress.Attempts++;

            if (status == ProgressStatus.Completed)
            {
                progress.CompletedAt = DateTime.UtcNow;
                progress.BestTimeSeconds = progress.BestTimeSeconds is null
                    ? request.ElapsedSeconds
                    : Math.Min(progress.BestTimeSeconds.Value, request.ElapsedSeconds);
            }
        }

        if (isNew)
            await progressRepository.AddAsync(progress, cancellationToken);
        else
            await progressRepository.UpdateAsync(progress, cancellationToken);

        return progress.ToResponse(includeFoundKeys: true);
    }

    public async Task<IReadOnlyList<SongPlayerResponse>> GetSongPlayersAsync(long songId, CancellationToken cancellationToken = default)
    {
        SongValidation.ValidateSongId(songId);

        var progress = await progressRepository.GetBySongAsync(songId, cancellationToken);

        return progress
            .OrderByDescending(p => p.BestPercentage)
            .ThenBy(p => p.BestTimeSeconds ?? int.MaxValue)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Select(p => p.ToSongPlayerResponse())
            .ToList();
    }

    private static Dictionary<string, int> CountWords(LyricsPuzzleResponse puzzle) =>
        puzzle.Lines
            .SelectMany(l => l.Tokens)
            .Where(t => t.Key is not null)
            .GroupBy(t => t.Key!)
            .ToDictionary(g => g.Key, g => g.Count());
}
