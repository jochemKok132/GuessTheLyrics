using GuessTheLyrics.Application.Caching;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GuessTheLyrics.Application;

public static class Dependencies
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services)
    {
        services.RegisterCaching();
        services.RegisterServices();
        return services;
    }

    private static void RegisterCaching(this IServiceCollection services)
    {
        services.AddSingleton<IPuzzleCache, PuzzleCache>();
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IPlayerService, PlayerService>();
    }
}
