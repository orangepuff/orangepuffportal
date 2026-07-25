using OrangepuffPortal.Config.Domain.Entity;

namespace OrangepuffPortal.Config.Domain.Repositories;

public interface IConfigRepository
{
    // Catalog
    Task<ConfigSection?> FindSectionAsync(string module, string textCode, CancellationToken cancellationToken = default);

    Task AddSectionAsync(ConfigSection section, CancellationToken cancellationToken = default);

    Task<ConfigItem?> FindConfigByCodeAsync(string configCode, CancellationToken cancellationToken = default);

    Task AddConfigAsync(ConfigItem config, CancellationToken cancellationToken = default);

    // Values
    Task<ConfigUser?> FindUserValueAsync(int userId, int configId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(ConfigUser Value, ConfigItem Config)>> ListUserValuesWithConfigAsync(int userId, CancellationToken cancellationToken = default);

    Task AddUserValueAsync(ConfigUser configUser, CancellationToken cancellationToken = default);

    Task AddUserValueHistoryAsync(ConfigUserHistory history, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
