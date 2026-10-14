using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.Infrastructure.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : BaseEntity
{
    // SQL Server error numbers for a unique index / unique constraint violation.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

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
        await SaveAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Set.Update(entity);
        await SaveAsync(entity, cancellationToken);
    }

    /// <summary>Soft delete: the row stays, the query filter hides it.</summary>
    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.DeletedAt = DateTime.UtcNow;
        Set.Update(entity);
        await SaveAsync(entity, cancellationToken);
    }

    /// <summary>
    /// SaveChanges that turns database errors into domain exceptions the API understands:
    /// a unique index violation (two requests creating the same user or progress row at once) becomes a
    /// <see cref="ConflictException"/> (409), a concurrency conflict too.
    /// </summary>
    private async Task SaveAsync(T entity, CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Context.Entry(entity).State = EntityState.Detached;
            throw new ConflictException("Someone else changed this at the same time. Reload and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Keep the context usable for the rest of the request.
            Context.Entry(entity).State = EntityState.Detached;
            throw new ConflictException("This already exists. Reload and try again.", ex);
        }
    }
}
