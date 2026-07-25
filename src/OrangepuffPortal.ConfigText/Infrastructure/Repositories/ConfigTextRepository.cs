using Microsoft.EntityFrameworkCore;
using OrangepuffPortal.ConfigText.Domain.Entity;
using OrangepuffPortal.ConfigText.Domain.Repositories;

namespace OrangepuffPortal.ConfigText.Infrastructure.Repositories;

public class ConfigTextRepository(ConfigTextDbContext db) : IConfigTextRepository
{
    public Task<ConfigTextDefinition?> FindAsync(string module, string textCode, string cultureCode, string textType, CancellationToken cancellationToken = default) =>
        db.ConfigTextDefinitions.FirstOrDefaultAsync(x =>
            x.Module == module && x.TextCode == textCode && x.CultureCode == cultureCode && x.TextType == textType, cancellationToken);

    public async Task<IReadOnlyList<ConfigTextDefinition>> GetForCultureAsync(string cultureCode, CancellationToken cancellationToken = default) =>
        await db.ConfigTextDefinitions
            .AsNoTracking()
            .Where(x => x.CultureCode == cultureCode || x.CultureCode == ConfigTextDefinition.WildcardCulture)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ConfigTextDefinition entry, CancellationToken cancellationToken = default) =>
        await db.ConfigTextDefinitions.AddAsync(entry, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
