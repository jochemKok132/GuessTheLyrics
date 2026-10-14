using GuessTheLyrics.Domain.Models;

namespace GuessTheLyrics.Domain.Interfaces;

/// <summary>Looks up songs and their lyrics in an external lyrics database.</summary>
public interface ILyricsProvider
{
    Task<IReadOnlyList<LyricsTrack>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <returns>The track, or <c>null</c> when the provider does not know the id.</returns>
    Task<LyricsTrack?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
