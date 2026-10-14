using System.Data.Common;
using System.Security.Authentication;
using System.Text.Json;
using GuessTheLyrics.API.Handlers;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GuessTheLyrics.UnitTests;

public class ApiErrorTests
{
    private sealed class FakeDbException(string message) : DbException(message);

    [Theory]
    [MemberData(nameof(Cases))]
    public void FromException_KnownException_MapsToStatusAndCode(Exception exception, int status, string code)
    {
        var error = ApiError.FromException(exception, requestAborted: false);

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.ErrorCode);
    }

    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new ArgumentException("Type at least 2 characters."), 400, ErrorCodes.Validation },
        { new JsonException("bad"), 400, ErrorCodes.Validation },
        { new AuthenticationException("Invalid username or password."), 401, ErrorCodes.Unauthorized },
        { new UnauthorizedAccessException(), 403, ErrorCodes.Forbidden },
        { new KeyNotFoundException("Song not found."), 404, ErrorCodes.NotFound },
        { new ConflictException("That username is already taken."), 409, ErrorCodes.Conflict },
        { new LyricsServiceException(LyricsServiceFailure.Unavailable, "503"), 503, ErrorCodes.LyricsUnavailable },
        { new LyricsServiceException(LyricsServiceFailure.RateLimited, "429"), 503, ErrorCodes.LyricsUnavailable },
        { new LyricsServiceException(LyricsServiceFailure.Timeout, "slow"), 504, ErrorCodes.LyricsTimeout },
        { new LyricsServiceException(LyricsServiceFailure.InvalidResponse, "html"), 502, ErrorCodes.LyricsInvalidResponse },
        { new DatabaseUnavailableException("down"), 503, ErrorCodes.DatabaseUnavailable },
        { new InvalidOperationException("wrapper", new FakeDbException("connection failed")), 503, ErrorCodes.DatabaseUnavailable },
        { new DbUpdateException("save failed", new FakeDbException("string truncated")), 500, ErrorCodes.Unexpected },
        { new TimeoutException(), 504, ErrorCodes.Timeout },
        { new ArgumentNullException("value"), 500, ErrorCodes.Unexpected },
        { new NullReferenceException(), 500, ErrorCodes.Unexpected }
    };

    [Fact]
    public void FromException_PlayerLeftDuringRequest_Returns499WithoutError()
    {
        var error = ApiError.FromException(new OperationCanceledException(), requestAborted: true);

        Assert.Equal(ApiError.ClientClosedRequest, error.StatusCode);
        Assert.Equal(LogLevel.Debug, error.LogLevel);
    }

    [Fact]
    public void FromException_CancelledWithoutPlayerLeaving_IsUnexpected()
    {
        var error = ApiError.FromException(new OperationCanceledException(), requestAborted: false);

        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public void FromException_Unexpected_NeverShowsTheExceptionMessage()
    {
        var error = ApiError.FromException(new InvalidOperationException("secret connection string"), requestAborted: false);

        Assert.DoesNotContain("secret", error.Message);
        Assert.Equal(LogLevel.Error, error.LogLevel);
    }

    [Fact]
    public void FromException_LyricsServiceDown_SuggestsRetryAfter()
    {
        var error = ApiError.FromException(
            new LyricsServiceException(LyricsServiceFailure.RateLimited, "429", TimeSpan.FromSeconds(20)), requestAborted: false);

        Assert.Equal(TimeSpan.FromSeconds(20), error.RetryAfter);
    }

    [Theory]
    [InlineData(404, ErrorCodes.NotFound)]
    [InlineData(405, ErrorCodes.MethodNotAllowed)]
    [InlineData(415, ErrorCodes.UnsupportedMediaType)]
    [InlineData(500, ErrorCodes.Unexpected)]
    public void FromStatusCode_EmptyErrorResponse_GetsCode(int status, string code)
    {
        Assert.Equal(code, ApiError.FromStatusCode(status).ErrorCode);
    }

    [Theory]
    [InlineData(ErrorCodes.LyricsUnavailable, true)]
    [InlineData(ErrorCodes.Network, true)]
    [InlineData(ErrorCodes.Validation, false)]
    [InlineData(ErrorCodes.NotFound, false)]
    [InlineData(null, false)]
    public void IsRetryable_ErrorCode_OnlyTemporaryProblems(string? code, bool expected)
    {
        Assert.Equal(expected, ErrorCodes.IsRetryable(code));
    }
}
