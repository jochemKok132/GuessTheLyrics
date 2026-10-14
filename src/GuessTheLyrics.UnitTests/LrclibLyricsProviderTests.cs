using System.Net;
using System.Text;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Infrastructure.Lrclib;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GuessTheLyrics.UnitTests;

public class LrclibLyricsProviderTests
{
    private const string TrackJson =
        """{"id":1,"trackName":"Song","artistName":"Artist","albumName":null,"duration":null,"instrumental":false,"plainLyrics":"la la"}""";

    /// <summary>Answers requests from a queue of responses (or exceptions) and counts the calls.</summary>
    private sealed class QueueHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _index;
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var next = responses[Math.Min(_index++, responses.Length - 1)];
            return Task.FromResult(next());
        }
    }

    private static Func<HttpResponseMessage> Status(HttpStatusCode status, string? json = null, TimeSpan? retryAfter = null) => () =>
    {
        var response = new HttpResponseMessage(status);
        if (json is not null)
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (retryAfter is not null)
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        return response;
    };

    private static Func<HttpResponseMessage> Throws(Exception exception) => () => throw exception;

    private static (LrclibLyricsProvider Provider, QueueHandler Handler) Create(params Func<HttpResponseMessage>[] responses)
    {
        var handler = new QueueHandler(responses);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.test/") };
        var options = Options.Create(new LrclibOptions
        {
            BaseUrl = "https://lrclib.test/",
            UserAgent = "tests",
            RetryCount = 2,
            RetryBaseDelayMilliseconds = 0,
            MaxRetryAfterSeconds = 3
        });
        return (new LrclibLyricsProvider(client, options, NullLogger<LrclibLyricsProvider>.Instance), handler);
    }

    [Fact]
    public async Task SearchAsync_Temporary503ThenSuccess_RetriesAndReturnsTracks()
    {
        var (sut, handler) = Create(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.OK, $"[{TrackJson}]"));

        var tracks = await sut.SearchAsync("song");

        Assert.Single(tracks);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task SearchAsync_Always503_ThrowsUnavailableAfterAllRetries()
    {
        var (sut, handler) = Create(Status(HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.Unavailable, exception.Failure);
        Assert.Equal(3, handler.Calls); // 1 attempt + 2 retries
    }

    [Fact]
    public async Task SearchAsync_429WithLongRetryAfter_GivesUpImmediately()
    {
        var (sut, handler) = Create(Status(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(60)));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.RateLimited, exception.Failure);
        Assert.Equal(TimeSpan.FromSeconds(60), exception.RetryAfter);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SearchAsync_Timeout_ThrowsTimeoutFailure()
    {
        var (sut, _) = Create(Throws(new TaskCanceledException("timeout")));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.Timeout, exception.Failure);
    }

    [Fact]
    public async Task SearchAsync_ConnectionError_ThrowsUnavailable()
    {
        var (sut, _) = Create(Throws(new HttpRequestException("connection refused")));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task SearchAsync_400_ThrowsInvalidResponseWithoutRetrying()
    {
        var (sut, handler) = Create(Status(HttpStatusCode.BadRequest));

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.InvalidResponse, exception.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SearchAsync_HtmlInsteadOfJson_ThrowsInvalidResponse()
    {
        var (sut, _) = Create(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "application/json")
        });

        var exception = await Assert.ThrowsAsync<LyricsServiceException>(() => sut.SearchAsync("song"));

        Assert.Equal(LyricsServiceFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task GetByIdAsync_404_ReturnsNull()
    {
        var (sut, _) = Create(Status(HttpStatusCode.NotFound, """{"code":404,"name":"TrackNotFound"}"""));

        var track = await sut.GetByIdAsync(1);

        Assert.Null(track);
    }

    [Fact]
    public async Task GetByIdAsync_NullDurationAndAlbum_MapsToDefaults()
    {
        var (sut, _) = Create(Status(HttpStatusCode.OK, TrackJson));

        var track = await sut.GetByIdAsync(1);

        Assert.NotNull(track);
        Assert.Equal(0, track!.Duration);
        Assert.Null(track.AlbumName);
    }
}
