using OrangepuffPortal.ConfigText.Domain.Entity;

namespace OrangepuffPortal.ConfigText.Domain.Repositories;

public interface IConfigTextRepository
{
    Task<ConfigTextDefinition?> FindAsync(string module, string textCode, string cultureCode, string textType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows at <paramref name="cultureCode"/> plus every wildcard row, for fallback resolution.
    /// </summary>
    Task<IReadOnlyList<ConfigTextDefinition>> GetForCultureAsync(string cultureCode, CancellationToken cancellationToken = default);

    Task AddAsync(ConfigTextDefinition entry, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
