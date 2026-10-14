using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.Infrastructure.Repositories;

public sealed class SongProgressRepository(AppDbContext context) : Repository<SongProgress>(context), ISongProgressRepository
{
    public Task<SongProgress?> GetAsync(Guid userId, long songId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(p => p.UserId == userId && p.SongId == songId, cancellationToken);

    public async Task<IReadOnlyList<SongProgress>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SongProgress>> GetBySongAsync(long songId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.SongId == songId)
            .ToListAsync(cancellationToken);
}
