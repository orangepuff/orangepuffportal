using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrangepuffPortal.ConfigData.Domain.Repositories;

namespace OrangepuffPortal.ConfigData.Infrastructure;

/// <summary>
/// Eagerly populates <see cref="ConfigDataCache"/> from the DB during application startup so the
/// cache is hot before the first request arrives. Runs on every app start and app pool recycle,
/// regardless of the <c>DoMigration</c> flag. Skips gracefully if pending migrations indicate the
/// schema is not yet ready (e.g. when <c>DoMigration</c> is false on first run).
/// </summary>
internal sealed class ConfigDataCacheWarmer(
    IServiceScopeFactory scopeFactory,
    ConfigDataCache cache,
    ILogger<ConfigDataCacheWarmer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string LogPrefix = nameof(ConfigDataCacheWarmer) + "." + nameof(StartAsync);

        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ConfigDataDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            logger.LogWarning("{LogPrefix}: skipping warm-up — {Count} pending migration(s) not yet applied", LogPrefix, pending.Count());
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IConfigDataRepository>();
        var all = await repository.GetAllAsync(cancellationToken);
        var rows = all.Select(e => new ConfigDataCacheRow(e.Id, e.Key, e.Value, e.AllowEditByScreen, e.Description)).ToList();
        await cache.SetAllAsync(rows, cancellationToken);

        logger.LogInformation("{LogPrefix}: warmed {Count} ConfigData entries", LogPrefix, rows.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
