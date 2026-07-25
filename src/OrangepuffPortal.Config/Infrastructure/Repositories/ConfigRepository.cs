using Microsoft.EntityFrameworkCore;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;

namespace OrangepuffPortal.Config.Infrastructure.Repositories;

public class ConfigRepository(ConfigDbContext db) : IConfigRepository
{
    public Task<ConfigSection?> FindSectionAsync(string module, string textCode, CancellationToken cancellationToken = default) =>
        db.ConfigSections.FirstOrDefaultAsync(x => x.Module == module && x.TextCode == textCode, cancellationToken);

    public async Task AddSectionAsync(ConfigSection section, CancellationToken cancellationToken = default) =>
        await db.ConfigSections.AddAsync(section, cancellationToken);

    public Task<ConfigItem?> FindConfigByCodeAsync(string configCode, CancellationToken cancellationToken = default) =>
        db.Configs.FirstOrDefaultAsync(x => x.ConfigCode == configCode, cancellationToken);

    public async Task AddConfigAsync(ConfigItem config, CancellationToken cancellationToken = default) =>
        await db.Configs.AddAsync(config, cancellationToken);

    public Task<ConfigUser?> FindUserValueAsync(int userId, int configId, CancellationToken cancellationToken = default) =>
        db.ConfigUsers.FirstOrDefaultAsync(x => x.UserId == userId && x.ConfigId == configId, cancellationToken);

    public async Task<IReadOnlyList<(ConfigUser Value, ConfigItem Config)>> ListUserValuesWithConfigAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.ConfigUsers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(db.Configs, cu => cu.ConfigId, c => c.Id, (cu, c) => new ValueTuple<ConfigUser, ConfigItem>(cu, c))
            .ToListAsync(cancellationToken);

    public async Task AddUserValueAsync(ConfigUser configUser, CancellationToken cancellationToken = default) =>
        await db.ConfigUsers.AddAsync(configUser, cancellationToken);

    public async Task AddUserValueHistoryAsync(ConfigUserHistory history, CancellationToken cancellationToken = default) =>
        await db.ConfigUsersHistory.AddAsync(history, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
