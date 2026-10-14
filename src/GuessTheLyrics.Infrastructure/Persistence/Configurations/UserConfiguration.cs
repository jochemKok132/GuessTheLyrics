using GuessTheLyrics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuessTheLyrics.Infrastructure.Persistence.Configurations;

public static class UserConfiguration
{
    public static void Configure(this EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(20).IsRequired();
        builder.Property(u => u.NormalizedUsername).HasMaxLength(20).IsRequired();
        builder.Property(u => u.HashedPassword).HasMaxLength(100).IsRequired();

        // Unique among active accounts; a soft-deleted account frees its name.
        builder.HasIndex(u => u.NormalizedUsername)
            .IsUnique()
            .HasFilter("[DeletedAt] IS NULL");

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
