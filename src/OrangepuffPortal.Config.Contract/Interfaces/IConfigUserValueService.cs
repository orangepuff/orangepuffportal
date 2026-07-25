namespace OrangepuffPortal.Config.Contract.Interfaces;

/// <summary>
/// Read/write path for a user's own config values. Unlike the catalog, these are live
/// application/user data — never seeded.
/// </summary>
public interface IConfigUserValueService
{
    Task<ConfigUserValueDto?> GetValueAsync(int userId, string configCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfigUserValueDto>> GetValuesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a user's value for <paramref name="configCode"/>. If a value already exists, its current
    /// (about-to-be-replaced) contents are snapshotted into ConfigUsersHistory first. Does not check
    /// Configs.BtAllowUserEdit — that is the caller's responsibility.
    /// </summary>
    Task SetValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken cancellationToken = default);
}
