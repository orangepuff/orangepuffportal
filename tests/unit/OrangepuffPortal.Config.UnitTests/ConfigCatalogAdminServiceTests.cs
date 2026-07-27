using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Contract.Interfaces;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;
using OrangepuffPortal.Config.Infrastructure;
using OrangepuffPortal.Shared.Translation;

namespace OrangepuffPortal.Config.UnitTests;

public class ConfigCatalogAdminServiceTests
{
    private static ConfigCatalogAdminService NewService(Mock<IConfigRepository> repo)
    {
        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, string _, CancellationToken _) => code);

        return new ConfigCatalogAdminService(
            repo.Object,
            Mock.Of<IConfigUserValueService>(),
            translation.Object,
            NullLogger<ConfigCatalogAdminService>.Instance);
    }

    [Fact]
    public async Task AddSectionAsync_defaults_sort_order_to_max_plus_one_when_not_given()
    {
        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ExistsSectionAsync("test.section", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetMaxSectionSortOrderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(4);

        ConfigSection? added = null;
        repo.Setup(r => r.AddSectionAsync(It.IsAny<ConfigSection>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigSection, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var service = NewService(repo);
        var request = new ConfigSectionUpsertRequest("Test Section", "test.section", true, ISortOrder: null);

        await service.AddSectionAsync(request, actorUserId: 1);

        Assert.NotNull(added);
        Assert.Equal(5, added!.SortOrder);
    }

    [Fact]
    public async Task AddSectionAsync_defaults_sort_order_to_one_when_no_sections_have_one_yet()
    {
        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ExistsSectionAsync("test.section", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetMaxSectionSortOrderAsync(It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        ConfigSection? added = null;
        repo.Setup(r => r.AddSectionAsync(It.IsAny<ConfigSection>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigSection, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var service = NewService(repo);
        var request = new ConfigSectionUpsertRequest("Test Section", "test.section", true, ISortOrder: null);

        await service.AddSectionAsync(request, actorUserId: 1);

        Assert.Equal(1, added!.SortOrder);
    }

    [Fact]
    public async Task AddSectionAsync_keeps_an_explicitly_given_sort_order()
    {
        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ExistsSectionAsync("test.section", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        ConfigSection? added = null;
        repo.Setup(r => r.AddSectionAsync(It.IsAny<ConfigSection>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigSection, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var service = NewService(repo);
        var request = new ConfigSectionUpsertRequest("Test Section", "test.section", true, ISortOrder: 99);

        await service.AddSectionAsync(request, actorUserId: 1);

        Assert.Equal(99, added!.SortOrder);
        repo.Verify(r => r.GetMaxSectionSortOrderAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddConfigAsync_defaults_sort_order_to_max_plus_one_within_the_same_section()
    {
        var section = new ConfigSection("Test Section", "test.section", true, DateTime.UtcNow);

        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.GetSectionByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(section);
        repo.Setup(r => r.ExistsConfigCodeAsync("test.code", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetMaxConfigSortOrderAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        ConfigItem? added = null;
        repo.Setup(r => r.AddConfigAsync(It.IsAny<ConfigItem>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigItem, CancellationToken>((c, _) => added = c)
            .Returns(Task.CompletedTask);

        var service = NewService(repo);
        var request = new ConfigItemUpsertRequest(1, "test.code", "Test", "test.textcode", (int)ConfigValueType.String, true, false, ISortOrder: null, null, null, null, null);

        await service.AddConfigAsync(request, actorUserId: 1);

        Assert.NotNull(added);
        Assert.Equal(3, added!.SortOrder);
    }
}
