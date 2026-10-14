using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Models;

namespace GuessTheLyrics.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerSummary>> GetPlayerSummariesAsync(CancellationToken cancellationToken = default);
}
