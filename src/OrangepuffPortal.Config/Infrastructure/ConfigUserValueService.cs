using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Contract.Interfaces;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;
using OrangepuffPortal.Shared.Auditing;

namespace OrangepuffPortal.Config.Infrastructure;

/// <summary>
/// Read/write path for a user's own config values — live application/user data, never seeded.
/// <see cref="SetValueAsync"/> snapshots the current value into ConfigUsersHistory before overwriting it.
/// </summary>
internal class ConfigUserValueService(IConfigRepository repository, ICurrentUser currentUser) : IConfigUserValueService
{
    public async Task<ConfigUserValueDto?> GetValueAsync(int userId, string configCode, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigOrThrowAsync(configCode, cancellationToken);
        var value = await repository.FindUserValueAsync(userId, config.Id, cancellationToken);
        return value is null ? null : ToDto(config, value);
    }

    public async Task<IReadOnlyList<ConfigUserValueDto>> GetValuesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await repository.ListUserValuesWithConfigAsync(userId, cancellationToken);
        return rows.Select(row => ToDto(row.Config, row.Value)).ToList();
    }

    public async Task<IReadOnlyList<UserConfigSectionDto>> GetSectionsForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await repository.ListVisibleCatalogWithUserValuesAsync(userId, cancellationToken);

        return rows
            .GroupBy(row => row.Section.Id)
            .Select(group =>
            {
                var section = group.First().Section;
                var items = group.Select(row => new UserConfigItemDto(
                    row.Item.ConfigCode,
                    row.Item.ConfigName,
                    row.Item.TextCode,
                    (ConfigValueType)row.Item.ConfigType,
                    row.Item.AllowUserEdit,
                    row.Item.SortOrder,
                    row.Value?.StringValue,
                    row.Value?.IntValue,
                    row.Value?.DecimalValue,
                    row.Value?.BoolValue)).ToList();

                return new UserConfigSectionDto(section.Module, section.SectionDesc, section.TextCode, section.SortOrder, items);
            })
            .ToList();
    }

    public async Task SetValueAsync(int userId, string configCode, ConfigValueInput value, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigOrThrowAsync(configCode, cancellationToken);
        var utcNow = DateTime.UtcNow;
        var actorUserId = currentUser.UserId;

        var existing = await repository.FindUserValueAsync(userId, config.Id, cancellationToken);

        if (existing is null)
        {
            var created = new ConfigUser(userId, config.Id, value.SConfigValue, value.IConfigValue, value.NConfigValue, value.BtConfigValue, actorUserId, utcNow);
            await repository.AddUserValueAsync(created, cancellationToken);
        }
        else
        {
            var snapshot = new ConfigUserHistory(
                existing.Id, existing.UserId, existing.ConfigId,
                existing.StringValue, existing.IntValue, existing.DecimalValue, existing.BoolValue, existing.Active,
                actorUserId, utcNow);
            await repository.AddUserValueHistoryAsync(snapshot, cancellationToken);

            existing.SetValue(value.SConfigValue, value.IConfigValue, value.NConfigValue, value.BtConfigValue, actorUserId, utcNow);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ConfigItem> GetConfigOrThrowAsync(string configCode, CancellationToken cancellationToken) =>
        await repository.FindConfigByCodeAsync(configCode, cancellationToken)
            ?? throw new InvalidOperationException($"Config '{configCode}' does not exist.");

    private static ConfigUserValueDto ToDto(ConfigItem config, ConfigUser value) =>
        new(config.ConfigCode, (ConfigValueType)config.ConfigType, value.StringValue, value.IntValue, value.DecimalValue, value.BoolValue);
}
