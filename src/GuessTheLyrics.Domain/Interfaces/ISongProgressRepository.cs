using GuessTheLyrics.Domain.Entities;

namespace GuessTheLyrics.Domain.Interfaces;

public interface ISongProgressRepository : IRepository<SongProgress>
{
    Task<SongProgress?> GetAsync(Guid userId, long songId, CancellationToken cancellationToken = default);

    /// <summary>All songs a player has played, most recently played first.</summary>
    Task<IReadOnlyList<SongProgress>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Everyone's progress on one song, including the player (<see cref="SongProgress.User"/> is loaded).</summary>
    Task<IReadOnlyList<SongProgress>> GetBySongAsync(long songId, CancellationToken cancellationToken = default);
}
