using Microsoft.Extensions.Logging;
using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Contract.Interfaces;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;

namespace OrangepuffPortal.Config.Infrastructure;

/// <summary>
/// Seeds a consuming app's default sections/configs into [config].[ConfigSections]/[Configs] at that
/// app's own startup. Insert-only unless a seed entry sets BtReplace — see the design doc
/// (docs/config-settings-design.md) for the full seeding contract.
/// </summary>
internal class ConfigCatalogWriter(IConfigRepository repository, ILogger<ConfigCatalogWriter> logger) : IConfigCatalogWriter
{
    public async Task UpsertAsync(string module, IReadOnlyCollection<ConfigSectionSeedEntry> sections, CancellationToken cancellationToken = default)
    {
        const string LogPrefix = nameof(ConfigCatalogWriter) + "." + nameof(UpsertAsync);

        var utcNow = DateTime.UtcNow;
        var sectionsWritten = 0;
        var configsWritten = 0;

        foreach (var sectionEntry in sections)
        {
            var section = await repository.FindSectionAsync(module, sectionEntry.STextCode, cancellationToken);

            if (section is null)
            {
                section = new ConfigSection(module, sectionEntry.SSectionDesc, sectionEntry.STextCode, sectionEntry.BtShow, utcNow, sectionEntry.ISortOrder);
                await repository.AddSectionAsync(section, cancellationToken);
                // Configs below need section.Id for their FK, so this one flushes early.
                await repository.SaveChangesAsync(cancellationToken);
                sectionsWritten++;
            }
            else if (sectionEntry.BtReplace)
            {
                section.Replace(sectionEntry.SSectionDesc, sectionEntry.BtShow, utcNow, sectionEntry.ISortOrder);
                sectionsWritten++;
            }

            foreach (var configEntry in sectionEntry.Configs)
            {
                var config = await repository.FindConfigByCodeAsync(configEntry.SConfigCode, cancellationToken);

                if (config is null)
                {
                    config = new ConfigItem(section.Id, configEntry.SConfigCode, configEntry.SConfigName, configEntry.STextCode, configEntry.IConfigType, configEntry.BtShow, configEntry.BtAllowUserEdit, utcNow, configEntry.ISortOrder);
                    await repository.AddConfigAsync(config, cancellationToken);
                    configsWritten++;
                }
                else if (configEntry.BtReplace)
                {
                    config.Replace(section.Id, configEntry.SConfigName, configEntry.STextCode, configEntry.IConfigType, configEntry.BtShow, configEntry.BtAllowUserEdit, utcNow, configEntry.ISortOrder);
                    configsWritten++;
                }
            }
        }

        if (sectionsWritten == 0 && configsWritten == 0)
        {
            logger.LogInformation("{LogPrefix}: nothing new to write for module {Module}", LogPrefix, module);
            return;
        }

        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "{LogPrefix}: module {Module} — wrote {Sections} section(s), {Configs} config(s)",
            LogPrefix, module, sectionsWritten, configsWritten);
    }
}
