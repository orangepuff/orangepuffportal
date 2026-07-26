using Microsoft.Extensions.Logging;
using OrangepuffPortal.ConfigText.Contract;
using OrangepuffPortal.ConfigText.Contract.Interfaces;
using OrangepuffPortal.ConfigText.Domain.Entity;
using OrangepuffPortal.ConfigText.Domain.Repositories;
using OrangepuffPortal.Shared.Paging;

namespace OrangepuffPortal.ConfigText.Infrastructure;

/// <summary>
/// Admin CRUD over [configtext].[ConfigTextDefinition], driven by a signed-in admin from the Bff —
/// unlike <see cref="ConfigTextWriter"/>, which is only ever run by unattended startup seeding.
/// </summary>
internal class ConfigTextAdminService(IConfigTextRepository repository, ConfigTextCache cache, ILogger<ConfigTextAdminService> logger) : IConfigTextAdminService
{
    public async Task<PagedResult<ConfigTextAdminDto>> ListAsync(
        string? module, string? textCode, string? cultureCode, string? textType, string? text, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, page - 1) * pageSize;
        var result = await repository.ListAsync(module, textCode, cultureCode, textType, text, skip, pageSize, cancellationToken);
        return new PagedResult<ConfigTextAdminDto>(result.Items.Select(ToDto).ToList(), result.TotalCount);
    }

    public async Task<ConfigTextAdminResult> AddAsync(ConfigTextAdminUpsertRequest request, int actorUserId, CancellationToken cancellationToken = default)
    {
        const string LogPrefix = nameof(ConfigTextAdminService) + "." + nameof(AddAsync);

        if (await repository.ExistsAsync(request.SModule, request.STextCode, request.SCultureCode, request.STextType, null, cancellationToken))
        {
            logger.LogWarning("{LogPrefix}: rejected, duplicate key {Module}/{TextCode}/{CultureCode}/{TextType}", LogPrefix, request.SModule, request.STextCode, request.SCultureCode, request.STextType);
            return ConfigTextAdminResult.Rejected("duplicate_key");
        }

        var utcNow = DateTime.UtcNow;
        var entry = new ConfigTextDefinition(request.SModule, request.STextCode, request.SCultureCode, request.STextType, request.SText, request.SNote, utcNow, actorUserId);
        await repository.AddAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        logger.LogInformation("{LogPrefix}: created row {Id}", LogPrefix, entry.Id);
        return ConfigTextAdminResult.Created(entry.Id);
    }

    public async Task<ConfigTextAdminResult> UpdateAsync(int id, ConfigTextAdminUpsertRequest request, int actorUserId, CancellationToken cancellationToken = default)
    {
        const string LogPrefix = nameof(ConfigTextAdminService) + "." + nameof(UpdateAsync);

        var entry = await repository.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            logger.LogWarning("{LogPrefix}: row {Id} not found", LogPrefix, id);
            return ConfigTextAdminResult.Rejected("not_found");
        }

        if (await repository.ExistsAsync(request.SModule, request.STextCode, request.SCultureCode, request.STextType, id, cancellationToken))
        {
            logger.LogWarning("{LogPrefix}: rejected, duplicate key {Module}/{TextCode}/{CultureCode}/{TextType}", LogPrefix, request.SModule, request.STextCode, request.SCultureCode, request.STextType);
            return ConfigTextAdminResult.Rejected("duplicate_key");
        }

        entry.AdminUpdate(request.SModule, request.STextCode, request.SCultureCode, request.STextType, request.SText, request.SNote, actorUserId, DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        logger.LogInformation("{LogPrefix}: updated row {Id}", LogPrefix, id);
        return ConfigTextAdminResult.Updated(id);
    }

    public async Task<ConfigTextAdminResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string LogPrefix = nameof(ConfigTextAdminService) + "." + nameof(DeleteAsync);

        var entry = await repository.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            logger.LogWarning("{LogPrefix}: row {Id} not found", LogPrefix, id);
            return ConfigTextAdminResult.Rejected("not_found");
        }

        await repository.DeleteAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        logger.LogInformation("{LogPrefix}: deleted row {Id}", LogPrefix, id);
        return ConfigTextAdminResult.Deleted();
    }

    private static ConfigTextAdminDto ToDto(ConfigTextDefinition entry) =>
        new(entry.Id, entry.Module, entry.TextCode, entry.CultureCode, entry.TextType, entry.Text, entry.Note, entry.InsertedTime, entry.UpdatedTime);
}
