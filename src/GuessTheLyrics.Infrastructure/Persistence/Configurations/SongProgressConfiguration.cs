using GuessTheLyrics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuessTheLyrics.Infrastructure.Persistence.Configurations;

public static class SongProgressConfiguration
{
    public static void Configure(this EntityTypeBuilder<SongProgress> builder)
    {
        builder.ToTable("SongProgress");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Artist).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Album).HasMaxLength(300);
        builder.Property(p => p.FoundKeys).IsRequired();
        builder.Property(p => p.Status).HasConversion<int>();

        builder.Ignore(p => p.Percentage);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Progress)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One progress row per player per song.
        builder.HasIndex(p => new { p.UserId, p.SongId })
            .IsUnique()
            .HasFilter("[DeletedAt] IS NULL");

        // "Who else played this song?"
        builder.HasIndex(p => p.SongId);

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
