using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrangepuffPortal.ConfigData.Domain.Repositories;

namespace OrangepuffPortal.ConfigData.Infrastructure;

/// <summary>
/// Eagerly populates <see cref="ConfigDataCache"/> from the DB during application startup so the
/// cache is hot before the first request arrives. Runs on every app start and app pool recycle,
/// regardless of the <c>DoMigration</c> flag.
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
        var repository = scope.ServiceProvider.GetRequiredService<IConfigDataRepository>();
        var all = await repository.GetAllAsync(cancellationToken);
        var rows = all.Select(e => new ConfigDataCacheRow(e.Id, e.Key, e.Value, e.AllowEditByScreen, e.Description)).ToList();
        await cache.SetAllAsync(rows, cancellationToken);

        logger.LogInformation("{LogPrefix}: warmed {Count} ConfigData entries", LogPrefix, rows.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
