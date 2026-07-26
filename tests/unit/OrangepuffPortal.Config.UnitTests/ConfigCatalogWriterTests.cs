using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;
using OrangepuffPortal.Config.Infrastructure;

namespace OrangepuffPortal.Config.UnitTests;

public class ConfigCatalogWriterTests
{
    private static Mock<IConfigRepository> NewRepoForBrandNewConfig(string configCode)
    {
        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.FindSectionAsync("TestModule", "test.section", It.IsAny<CancellationToken>())).ReturnsAsync((ConfigSection?)null);
        repo.Setup(r => r.FindConfigByCodeAsync(configCode, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigItem?)null);
        return repo;
    }

    private static ConfigSectionSeedEntry SectionWith(ConfigSeedEntry config) =>
        new("Test Section", "test.section", [config]);

    [Fact]
    public async Task UpsertAsync_backfills_default_onto_existing_users_for_a_brand_new_config()
    {
        var repo = NewRepoForBrandNewConfig("test.newConfig");

        List<ConfigUser>? backfilled = null;
        repo.Setup(r => r.AddUserValuesAsync(It.IsAny<IReadOnlyCollection<ConfigUser>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<ConfigUser>, CancellationToken>((values, _) => backfilled = values.ToList())
            .Returns(Task.CompletedTask);

        var writer = new ConfigCatalogWriter(repo.Object, NullLogger<ConfigCatalogWriter>.Instance);
        var configEntry = new ConfigSeedEntry("test.newConfig", "New Config", "test.newconfig.textcode", (int)ConfigValueType.Int, DefaultValue: new ConfigValueInput(IConfigValue: 42));

        await writer.UpsertAsync("TestModule", [SectionWith(configEntry)], existingUserIds: [1, 2, 3]);

        Assert.NotNull(backfilled);
        Assert.Equal(3, backfilled!.Count);
        Assert.All(backfilled, u => Assert.Equal(42, u.IntValue));
        Assert.All(backfilled, u => Assert.Equal(0, u.InsertedUserId)); // system actor — no real actor at unattended startup
        Assert.Equal([1, 2, 3], backfilled.Select(u => u.UserId).OrderBy(id => id));
    }

    [Fact]
    public async Task UpsertAsync_does_not_backfill_when_no_existing_user_ids_are_given()
    {
        var repo = NewRepoForBrandNewConfig("test.newConfig");

        var writer = new ConfigCatalogWriter(repo.Object, NullLogger<ConfigCatalogWriter>.Instance);
        var configEntry = new ConfigSeedEntry("test.newConfig", "New Config", "test.newconfig.textcode", (int)ConfigValueType.Int, DefaultValue: new ConfigValueInput(IConfigValue: 42));

        await writer.UpsertAsync("TestModule", [SectionWith(configEntry)]);

        repo.Verify(r => r.AddUserValuesAsync(It.IsAny<IReadOnlyCollection<ConfigUser>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_does_not_backfill_a_config_with_no_default_value()
    {
        var repo = NewRepoForBrandNewConfig("test.newConfig");

        var writer = new ConfigCatalogWriter(repo.Object, NullLogger<ConfigCatalogWriter>.Instance);
        var configEntry = new ConfigSeedEntry("test.newConfig", "New Config", "test.newconfig.textcode", (int)ConfigValueType.Int);

        await writer.UpsertAsync("TestModule", [SectionWith(configEntry)], existingUserIds: [1, 2, 3]);

        repo.Verify(r => r.AddUserValuesAsync(It.IsAny<IReadOnlyCollection<ConfigUser>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_does_not_retroactively_backfill_when_replacing_an_already_seeded_config()
    {
        var existingConfig = new ConfigItem(1, "test.existing", "Existing", "test.existing.textcode", (int)ConfigValueType.Int, true, false, DateTime.UtcNow);

        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.FindSectionAsync("TestModule", "test.section", It.IsAny<CancellationToken>())).ReturnsAsync((ConfigSection?)null);
        repo.Setup(r => r.FindConfigByCodeAsync("test.existing", It.IsAny<CancellationToken>())).ReturnsAsync(existingConfig);

        var writer = new ConfigCatalogWriter(repo.Object, NullLogger<ConfigCatalogWriter>.Instance);
        var configEntry = new ConfigSeedEntry(
            "test.existing", "Existing", "test.existing.textcode", (int)ConfigValueType.Int,
            DefaultValue: new ConfigValueInput(IConfigValue: 42), BtReplace: true);

        await writer.UpsertAsync("TestModule", [SectionWith(configEntry)], existingUserIds: [1, 2, 3]);

        repo.Verify(r => r.AddUserValuesAsync(It.IsAny<IReadOnlyCollection<ConfigUser>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(42, existingConfig.DefaultIntValue); // the stored default is still updated, for future new users/configs
    }
}
