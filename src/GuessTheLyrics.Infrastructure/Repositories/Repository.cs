using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.Infrastructure.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : BaseEntity
{
    protected AppDbContext Context { get; } = context;
    protected DbSet<T> Set => Context.Set<T>();

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();

        entity.CreatedAt = DateTime.UtcNow;
        await Set.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Set.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Soft delete: the row stays, the query filter hides it.</summary>
    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.DeletedAt = DateTime.UtcNow;
        Set.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }
}
