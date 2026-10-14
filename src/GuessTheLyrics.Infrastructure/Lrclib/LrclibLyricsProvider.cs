using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuessTheLyrics.Infrastructure.Lrclib;

/// <summary>
/// Lyrics from LRCLIB (https://lrclib.net), a free, open lyrics database that needs no API key.
/// Temporary failures (5xx such as "503 Service Unavailable", 429, timeouts, connection errors) are retried
/// with a short backoff; when that doesn't help a <see cref="LyricsServiceException"/> is thrown, which the API
/// turns into a clear "try again" message instead of a 500.
/// </summary>
public sealed class LrclibLyricsProvider(
    HttpClient httpClient,
    IOptions<LrclibOptions> options,
    ILogger<LrclibLyricsProvider> logger) : ILyricsProvider
{
    private readonly LrclibOptions _options = options.Value;

    public async Task<IReadOnlyList<LyricsTrack>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync($"api/search?q={Uri.EscapeDataString(query)}", notFoundIsValid: false, cancellationToken);
        var tracks = await ReadJsonAsync<List<LrclibTrackDto>>(response, cancellationToken) ?? [];

        return tracks.Select(ToDomain).ToList();
    }

    public async Task<LyricsTrack?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync($"api/get/{id}", notFoundIsValid: true, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var track = await ReadJsonAsync<LrclibTrackDto>(response, cancellationToken);
        return track is null ? null : ToDomain(track);
    }

    /// <summary>GET with retries. Returns a successful response (or a 404 when <paramref name="notFoundIsValid"/>).</summary>
    private async Task<HttpResponseMessage> SendAsync(string url, bool notFoundIsValid, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            LyricsServiceFailure failure;
            TimeSpan? retryAfter = null;
            Exception? error = null;

            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode || (notFoundIsValid && response.StatusCode == HttpStatusCode.NotFound))
                    return response;

                var status = (int)response.StatusCode;
                retryAfter = GetRetryAfter(response);
                response.Dispose();

                if (status == (int)HttpStatusCode.TooManyRequests)
                {
                    failure = LyricsServiceFailure.RateLimited;
                }
                else if (status >= 500)
                {
                    failure = LyricsServiceFailure.Unavailable;
                }
                else
                {
                    // Other 4xx: our request was wrong for LRCLIB; retrying the same request won't help.
                    logger.LogError("LRCLIB answered {StatusCode} for {Url}", status, url);
                    throw new LyricsServiceException(LyricsServiceFailure.InvalidResponse, $"LRCLIB answered {status} for {url}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // the player left; not a lyrics service problem
            }
            catch (TaskCanceledException ex)
            {
                failure = LyricsServiceFailure.Timeout; // HttpClient.Timeout elapsed
                error = ex;
            }
            catch (HttpRequestException ex)
            {
                failure = LyricsServiceFailure.Unavailable; // DNS, connection refused, TLS, reset...
                error = ex;
            }

            var waitTooLong = retryAfter > TimeSpan.FromSeconds(_options.MaxRetryAfterSeconds);
            if (attempt >= _options.RetryCount || waitTooLong)
            {
                logger.LogWarning(error, "LRCLIB {Url} failed ({Failure}) after {Attempts} attempt(s)", url, failure, attempt + 1);
                throw new LyricsServiceException(failure, $"LRCLIB {url} failed: {failure}.", retryAfter, error);
            }

            var delay = retryAfter ?? TimeSpan.FromMilliseconds(_options.RetryBaseDelayMilliseconds * Math.Pow(2, attempt));
            logger.LogInformation("LRCLIB {Url} failed ({Failure}); retry {Retry}/{RetryCount} in {Delay} ms",
                url, failure, attempt + 1, _options.RetryCount, (int)delay.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Not JSON, or JSON in a shape we don't understand (e.g. an HTML error page from a proxy).
            logger.LogError(ex, "LRCLIB sent a response that could not be read as {Type}", typeof(T).Name);
            throw new LyricsServiceException(LyricsServiceFailure.InvalidResponse, "LRCLIB sent an unreadable response.", innerException: ex);
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
            return delta;
        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        return null;
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
