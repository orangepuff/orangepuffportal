namespace OrangepuffPortal.ConfigText.Contract.Interfaces;

/// <summary>
/// Read path for the shared ConfigTextDefinition table, with culture fallback already resolved.
/// </summary>
public interface IConfigTextReader
{
    /// <summary>
    /// Returns one resolved row per (SModule, STextCode, STextType): the row matching
    /// <paramref name="cultureCode"/> if one exists, otherwise the "*" fallback row.
    /// </summary>
    Task<IReadOnlyList<ConfigTextEntryDto>> GetAllAsync(
        string cultureCode,
        CancellationToken cancellationToken = default);
}
