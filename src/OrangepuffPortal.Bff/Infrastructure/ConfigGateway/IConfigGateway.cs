using OrangepuffPortal.Config.Contract;

namespace OrangepuffPortal.Bff.Infrastructure.ConfigGateway
{
    /// <summary>
    /// Facade the Bff uses to reach the Config module, in-process.
    /// </summary>
    public interface IConfigGateway
    {
        Task<IReadOnlyList<UserConfigSectionDto>> GetSectionsForUserAsync(int userId, CancellationToken ct = default);
        Task SetConfigValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken ct = default);
    }
}
