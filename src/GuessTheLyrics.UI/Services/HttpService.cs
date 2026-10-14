using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GuessTheLyrics.Shared;
using Microsoft.Extensions.Logging;

namespace GuessTheLyrics.UI.Services;

/// <summary>
/// All API calls go through here and always come back as an <see cref="ApiResponse{T}"/>, never as an exception:
/// the API's own error envelope when there is one, otherwise a message and <see cref="ErrorCodes">error code</see>
/// based on what went wrong (no connection, timeout, a proxy error page...).
/// </summary>
public sealed class HttpService(HttpClient httpClient, ILogger<HttpService> logger) : IHttpService
{
    public Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, url, () => httpClient.GetAsync(url, cancellationToken), cancellationToken);

    public Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Post, url, () => httpClient.PostAsJsonAsync(url, body, cancellationToken), cancellationToken);

    public Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Put, url, () => httpClient.PutAsJsonAsync(url, body, cancellationToken), cancellationToken);

    private async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string url, Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();

            // The API answers every request, errors included, with the ApiResponse envelope.
            var body = await TryReadEnvelopeAsync<T>(response, cancellationToken);
            if (body is not null)
                return body;

            // No envelope: something between us and the API answered (nginx error page, server restarting...).
            logger.LogWarning("{Method} {Url} returned {StatusCode} without an API response body", method, url, (int)response.StatusCode);
            return FromStatus<T>(response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the page cancelled the call on purpose
        }
        catch (TaskCanceledException)
        {
            return ApiResponse<T>.Fail("The server took too long to answer. Try again.", ErrorCodes.Timeout);
        }
        catch (HttpRequestException ex)
        {
            // In the browser this covers: offline, DNS, refused connection, CORS blocked.
            logger.LogWarning(ex, "{Method} {Url} could not reach the API", method, url);
            return ApiResponse<T>.Fail("Could not reach the server. Check your internet connection and try again.", ErrorCodes.Network);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Method} {Url} failed unexpectedly", method, url);
            return ApiResponse<T>.Fail("Something went wrong. Try again.", ErrorCodes.Unexpected);
        }
    }

    private static ApiResponse<T> FromStatus<T>(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => ApiResponse<T>.Fail("The server sent an empty answer. Try again.", ErrorCodes.Unexpected),
        400 => ApiResponse<T>.Fail("The request was not valid.", ErrorCodes.Validation),
        401 => ApiResponse<T>.Fail("You need to be logged in.", ErrorCodes.Unauthorized),
        403 => ApiResponse<T>.Fail("You are not allowed to do this.", ErrorCodes.Forbidden),
        404 => ApiResponse<T>.Fail("Not found.", ErrorCodes.NotFound),
        408 or 504 => ApiResponse<T>.Fail("The server took too long to answer. Try again.", ErrorCodes.Timeout),
        429 => ApiResponse<T>.Fail("Too many requests. Wait a minute and try again.", ErrorCodes.RateLimited),
        502 or 503 => ApiResponse<T>.Fail("The server is temporarily unavailable (maybe an update is being installed). Try again in a minute.", ErrorCodes.ServerUnavailable),
        _ => ApiResponse<T>.Fail("Something went wrong on the server. Try again.", ErrorCodes.Unexpected)
    };

    private static async Task<ApiResponse<T>?> TryReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType || !mediaType.Contains("json"))
            return null;

        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);

            // A JSON body that isn't our envelope (e.g. ProblemDetails) has neither a value nor a message.
            if (body is null || (!body.Succeeded && body.Message is null))
                return null;

            return body;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
