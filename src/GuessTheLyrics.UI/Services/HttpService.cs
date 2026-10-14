using System.Net.Http.Json;
using System.Text.Json;
using GuessTheLyrics.Shared;

namespace GuessTheLyrics.UI.Services;

public sealed class HttpService(HttpClient httpClient) : IHttpService
{
    private const string UnexpectedError = "An unexpected error occurred.";
    private const string ApiUnreachable = "Could not reach the GuessTheLyrics API. Is it running?";

    public Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => httpClient.GetAsync(url, cancellationToken), cancellationToken);

    public Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => httpClient.PostAsJsonAsync(url, body, cancellationToken), cancellationToken);

    public Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => httpClient.PutAsJsonAsync(url, body, cancellationToken), cancellationToken);

    private static async Task<ApiResponse<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();

            // Every API response (errors included) uses the ApiResponse envelope,
            // so read it even for 4xx/5xx to show the API's own message.
            var body = await TryReadEnvelopeAsync<T>(response, cancellationToken);
            return body ?? ApiResponse<T>.Fail(UnexpectedError);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return ApiResponse<T>.Fail(ApiUnreachable);
        }
        catch (Exception)
        {
            return ApiResponse<T>.Fail(UnexpectedError);
        }
    }

    private static async Task<ApiResponse<T>?> TryReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            // Not JSON (e.g. an HTML error page from a proxy).
            return null;
        }
    }
}
