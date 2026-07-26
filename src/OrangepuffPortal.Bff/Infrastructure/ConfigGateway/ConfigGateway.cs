using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Contract.Interfaces;

namespace OrangepuffPortal.Bff.Infrastructure.ConfigGateway
{
    /// <summary>
    /// In-process <see cref="IConfigGateway"/> — the Config module has no MediatR handlers (unlike
    /// Identity), so this just forwards straight to <see cref="IConfigUserValueService"/>.
    /// </summary>
    public class ConfigGateway(IConfigUserValueService service) : IConfigGateway
    {
        public Task<IReadOnlyList<UserConfigSectionDto>> GetSectionsForUserAsync(int userId, CancellationToken ct = default) =>
            service.GetSectionsForUserAsync(userId, ct);

        public Task SetConfigValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken ct = default) =>
            service.SetValueAsync(userId, configCode, value, ct);
    }
}
