namespace OrangepuffPortal.Config.Contract.Interfaces;

/// <summary>
/// Push path for a consuming app's own default sections/configs into the shared config catalog.
/// Called in-process at the consuming app's own startup, not over HTTP — mirrors
/// <c>IConfigTextWriter</c>'s insert-only/BtReplace seeding contract.
/// </summary>
public interface IConfigCatalogWriter
{
    /// <summary>
    /// Upserts <paramref name="module"/>'s sections and their nested configs. A section already
    /// present (matched by module + STextCode) is left untouched unless it sets BtReplace; same for
    /// each config within it (matched by SConfigCode alone, globally).
    /// </summary>
    Task UpsertAsync(
        string module,
        IReadOnlyCollection<ConfigSectionSeedEntry> sections,
        CancellationToken cancellationToken = default);
}
