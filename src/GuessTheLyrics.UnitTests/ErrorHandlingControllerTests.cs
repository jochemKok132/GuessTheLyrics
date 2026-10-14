using GuessTheLyrics.API.Controllers;
using GuessTheLyrics.Domain.Exceptions;
using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GuessTheLyrics.UnitTests;

public class ErrorHandlingControllerTests
{
    /// <summary>Exposes the protected helpers for testing.</summary>
    private sealed class TestController : ErrorHandlingController
    {
        public Task<ActionResult<ApiResponse<T>>> Run<T>(Func<Task<T>> action) => HandleAsync(action);
        public ActionResult<ApiResponse<T>> RunSync<T>(Func<T> action) => Handle(action);
    }

    private static TestController CreateController()
    {
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOkWithValue()
    {
        var controller = CreateController();

        var result = await controller.Run(() => Task.FromResult(42));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<int>>(ok.Value);
        Assert.True(body.Succeeded);
        Assert.Equal(42, body.Value);
    }

    [Fact]
    public async Task HandleAsync_LyricsServiceDown_Returns503WithRetryAfterInsteadOfThrowing()
    {
        var controller = CreateController();

        var result = await controller.Run<int>(() =>
            throw new LyricsServiceException(LyricsServiceFailure.Unavailable, "LRCLIB down", TimeSpan.FromSeconds(2)));

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, error.StatusCode);
        var body = Assert.IsType<ApiResponse<int>>(error.Value);
        Assert.False(body.Succeeded);
        Assert.Equal(ErrorCodes.LyricsUnavailable, body.ErrorCode);
        Assert.False(string.IsNullOrEmpty(body.TraceId));
        Assert.Equal("2", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void Handle_NotFound_Returns404()
    {
        var controller = CreateController();

        var result = controller.RunSync<int>(() => throw new KeyNotFoundException("Match not found."));

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Match not found.", Assert.IsType<ApiResponse<int>>(error.Value).Message);
    }

    [Fact]
    public void Handle_Bug_Returns500WithoutInternalMessage()
    {
        var controller = CreateController();

        var result = controller.RunSync<int>(() => throw new InvalidOperationException("connection string secret"));

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("secret", Assert.IsType<ApiResponse<int>>(error.Value).Message!);
    }
}
