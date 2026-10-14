using Microsoft.JSInterop;

namespace GuessTheLyrics.UI.Auth;

public interface ILocalStorageService
{
    ValueTask<string?> GetAsync(string key);
    ValueTask SetAsync(string key, string value);
    ValueTask RemoveAsync(string key);
}

/// <summary>localStorage, so you stay logged in after closing the tab.</summary>
public sealed class LocalStorageService(IJSRuntime js) : ILocalStorageService
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", key);
    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("localStorage.setItem", key, value);
    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("localStorage.removeItem", key);
}
