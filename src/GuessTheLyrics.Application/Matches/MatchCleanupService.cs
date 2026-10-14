using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuessTheLyrics.Application.Matches;

/// <summary>Every minute: forget abandoned lobbies and finished matches, so memory doesn't grow.</summary>
public sealed class MatchCleanupService(MatchStore store, TimeProvider timeProvider, ILogger<MatchCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = store.RemoveExpired(timeProvider.GetUtcNow().UtcDateTime);
                if (removed > 0)
                    logger.LogInformation("Removed {Count} expired match(es)", removed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Match cleanup failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
