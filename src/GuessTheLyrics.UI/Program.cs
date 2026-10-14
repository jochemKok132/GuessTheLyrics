using GuessTheLyrics.UI;
using GuessTheLyrics.UI.Auth;
using GuessTheLyrics.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// wwwroot/appsettings.json → "ApiBaseUrl". Relative to where the app is served, so an empty value
// (appsettings.Production.json) means "same origin and path as the UI", e.g. https://jochemkok.nl/guessthelyrics/.
var apiBaseUrl = new Uri(
    new Uri(builder.HostEnvironment.BaseAddress),
    builder.Configuration.GetValue<string>("ApiBaseUrl") ?? string.Empty);

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthTokenHandler>();

// Every API call goes through the token handler, which adds the bearer token when logged in.
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<AuthTokenHandler>()) { BaseAddress = apiBaseUrl });
builder.Services.AddScoped<IHttpService, HttpService>();

// Feature services
builder.Services.AddScoped<ISongService, SongService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IMatchClient, MatchClient>();

await builder.Build().RunAsync();
