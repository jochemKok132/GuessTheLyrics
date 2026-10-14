using System.Net;
using System.Net.Http.Json;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;

namespace GuessTheLyrics.Infrastructure.Lrclib;

/// <summary>
/// Lyrics from LRCLIB (https://lrclib.net), a free, open lyrics database that needs no API key.
/// </summary>
public sealed class LrclibLyricsProvider(HttpClient httpClient) : ILyricsProvider
{
    public async Task<IReadOnlyList<LyricsTrack>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"api/search?q={Uri.EscapeDataString(query)}";
        var tracks = await httpClient.GetFromJsonAsync<List<LrclibTrackDto>>(url, cancellationToken) ?? [];

        return tracks.Select(ToDomain).ToList();
    }

    public async Task<LyricsTrack?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/get/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var track = await response.Content.ReadFromJsonAsync<LrclibTrackDto>(cancellationToken);
        return track is null ? null : ToDomain(track);
    }

    private static LyricsTrack ToDomain(LrclibTrackDto dto) => new()
    {
        Id = dto.Id,
        TrackName = dto.TrackName ?? "Unknown title",
        ArtistName = dto.ArtistName ?? "Unknown artist",
        AlbumName = dto.AlbumName,
        Duration = dto.Duration ?? 0,
        Instrumental = dto.Instrumental ?? false,
        PlainLyrics = dto.PlainLyrics
    };
}
