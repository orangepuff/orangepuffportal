using OrangepuffPortal.ConfigText.Contract;
using OrangepuffPortal.ConfigText.Contract.Interfaces;
using OrangepuffPortal.ConfigText.Domain.Repositories;

namespace OrangepuffPortal.ConfigText.Infrastructure;

/// <summary>
/// Resolves each (module, code, type) key to a single row: the exact-culture row if one exists, otherwise the "*" fallback row. Backed by <see cref="ConfigTextCache"/>.
/// </summary>
internal class ConfigTextReader(IConfigTextRepository repository, ConfigTextCache cache) : IConfigTextReader
{
    public async Task<IReadOnlyList<ConfigTextEntryDto>> GetAllAsync(string cultureCode, CancellationToken cancellationToken = default)
    {
        var rows = await cache.GetOrCreateAsync(cultureCode, () => repository.GetForCultureAsync(cultureCode, cancellationToken));

        return rows
            .GroupBy(x => (x.Module, x.TextCode, x.TextType))
            .Select(g => g.OrderBy(x => x.CultureCode == cultureCode ? 0 : 1).First())
            .Select(x => new ConfigTextEntryDto(x.Module, x.TextCode, x.TextType, x.Text))
            .ToList();
    }
}
