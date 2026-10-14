using GuessTheLyrics.Shared;

namespace GuessTheLyrics.UI.Services;

public interface IHttpService
{
    Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default);
    Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
    Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken cancellationToken = default);
}
