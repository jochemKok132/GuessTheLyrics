using GuessTheLyrics.Domain.Enums;

namespace GuessTheLyrics.Domain.Entities;

/// <summary>One player's progress on one song: the current attempt plus their best result.</summary>
public sealed class SongProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>LRCLIB track id.</summary>
    public long SongId { get; set; }

    // Copied when the song is first played, so progress lists never need the lyrics service.
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public string? Album { get; set; }
    public int TotalWords { get; set; }
    public int UniqueWords { get; set; }

    // ----- Current attempt -----

    /// <summary>Normalised words found in the current attempt, separated by spaces (keys contain only letters and digits).</summary>
    public string FoundKeys { get; set; } = string.Empty;

    /// <summary>Found words in the current attempt, repeats included.</summary>
    public int FoundWordCount { get; set; }

    public int ElapsedSeconds { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.InProgress;

    // ----- Best result over all attempts -----

    /// <summary>Number of finished attempts (completed or given up).</summary>
    public int Attempts { get; set; }

    public int BestPercentage { get; set; }

    /// <summary>Fastest completion in seconds, <c>null</c> until the song has been completed once.</summary>
    public int? BestTimeSeconds { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int Percentage => TotalWords == 0 ? 0 : FoundWordCount * 100 / TotalWords;
}
