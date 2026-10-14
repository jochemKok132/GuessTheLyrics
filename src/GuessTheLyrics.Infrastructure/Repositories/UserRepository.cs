using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;
using GuessTheLyrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.Infrastructure.Repositories;

public sealed class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> ExistsAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

    public async Task<IReadOnlyList<PlayerSummary>> GetPlayerSummariesAsync(CancellationToken cancellationToken = default) =>
        await Summaries(Set.AsNoTracking()).ToListAsync(cancellationToken);

    // One SQL query: every user with their aggregated progress.
    private static IQueryable<PlayerSummary> Summaries(IQueryable<User> users) =>
        users.Select(u => new PlayerSummary(
            u.Id,
            u.Username,
            u.CreatedAt,
            u.Progress.Count(),
            u.Progress.Count(p => p.BestTimeSeconds != null),
            u.Progress.Sum(p => p.FoundWordCount),
            u.Progress.Any() ? u.Progress.Average(p => (double)p.BestPercentage) : 0,
            u.Progress.Max(p => (DateTime?)(p.UpdatedAt ?? p.CreatedAt))));
}
