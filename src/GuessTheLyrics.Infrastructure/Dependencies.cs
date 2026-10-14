using GuessTheLyrics.Domain.Interfaces;
using GuessTheLyrics.Infrastructure.Lrclib;
using GuessTheLyrics.Infrastructure.Persistence;
using GuessTheLyrics.Infrastructure.Repositories;
using GuessTheLyrics.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GuessTheLyrics.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterPersistence(configuration);
        services.RegisterRepositories();
        services.RegisterSecurity();
        services.RegisterLyricsProvider(configuration);
        return services;
    }

    private static void RegisterPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("Migrations");
                sql.EnableRetryOnFailure();
            }));
    }

    private static void RegisterRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISongProgressRepository, SongProgressRepository>();
    }

    private static void RegisterSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPassword, BCryptPassword>();
    }

    private static void RegisterLyricsProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LrclibOptions>()
            .Bind(configuration.GetSection(LrclibOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Typed HttpClient: the factory manages handler lifetimes, the provider just uses it.
        services.AddHttpClient<ILyricsProvider, LrclibLyricsProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<LrclibOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            // LRCLIB asks clients to identify themselves with a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });
    }
}
