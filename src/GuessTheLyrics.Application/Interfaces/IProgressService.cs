using GuessTheLyrics.Shared.Progress;

namespace GuessTheLyrics.Application.Interfaces;

public interface IProgressService
{
    /// <returns>The player's progress on the song, or <c>null</c> when they never played it.</returns>
    Task<SongProgressResponse?> GetMyProgressAsync(Guid userId, long songId, CancellationToken cancellationToken = default);

    Task<SongProgressResponse> SaveAsync(Guid userId, long songId, SaveProgressRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SongPlayerResponse>> GetSongPlayersAsync(long songId, CancellationToken cancellationToken = default);
}
