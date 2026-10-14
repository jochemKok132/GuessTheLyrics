using GuessTheLyrics.Application.Caching;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Matches;
using GuessTheLyrics.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GuessTheLyrics.Application;

public static class Dependencies
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services)
    {
        services.RegisterCaching();
        services.RegisterServices();
        services.RegisterMatches();
        return services;
    }

    private static void RegisterCaching(this IServiceCollection services)
    {
        services.AddSingleton<IPuzzleCache, PuzzleCache>();
    }

    private static void RegisterMatches(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MatchStore>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddHostedService<MatchCleanupService>();
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IPlayerService, PlayerService>();
    }
}
