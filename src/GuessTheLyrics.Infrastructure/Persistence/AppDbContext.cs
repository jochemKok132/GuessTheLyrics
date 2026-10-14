using GuessTheLyrics.Domain.Entities;
using GuessTheLyrics.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SongProgress> Progress => Set<SongProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().Configure();
        modelBuilder.Entity<SongProgress>().Configure();
    }
}
