using Moq;
using OrangepuffPortal.Config.Contract;
using OrangepuffPortal.Config.Domain.Entity;
using OrangepuffPortal.Config.Domain.Repositories;
using OrangepuffPortal.Config.Infrastructure;
using OrangepuffPortal.Shared.Auditing;

namespace OrangepuffPortal.Config.UnitTests;

public class ConfigUserValueServiceTests
{
    [Fact]
    public async Task ApplyDefaultsForNewUserAsync_inserts_a_value_for_a_config_with_a_default()
    {
        var config = new ConfigItem(1, "test.code", "Test", "test.textcode", (int)ConfigValueType.String, true, false, DateTime.UtcNow, defaultStringValue: "the-default");

        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ListConfigsWithDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync([config]);
        repo.Setup(r => r.FindUserValueAsync(42, config.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigUser?)null);

        var service = new ConfigUserValueService(repo.Object, Mock.Of<ICurrentUser>());

        await service.ApplyDefaultsForNewUserAsync(42, actorUserId: 0, CancellationToken.None);

        repo.Verify(r => r.AddUserValueAsync(
            It.Is<ConfigUser>(u => u.UserId == 42 && u.ConfigId == config.Id && u.StringValue == "the-default" && u.InsertedUserId == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyDefaultsForNewUserAsync_skips_a_config_the_user_already_has_a_value_for()
    {
        var config = new ConfigItem(1, "test.code", "Test", "test.textcode", (int)ConfigValueType.String, true, false, DateTime.UtcNow, defaultStringValue: "the-default");
        var existingValue = new ConfigUser(42, config.Id, "already-set", null, null, null, actorUserId: 42, DateTime.UtcNow);

        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ListConfigsWithDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync([config]);
        repo.Setup(r => r.FindUserValueAsync(42, config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingValue);

        var service = new ConfigUserValueService(repo.Object, Mock.Of<ICurrentUser>());

        await service.ApplyDefaultsForNewUserAsync(42, actorUserId: 0, CancellationToken.None);

        repo.Verify(r => r.AddUserValueAsync(It.IsAny<ConfigUser>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyDefaultsForNewUserAsync_does_nothing_when_no_configs_have_a_default()
    {
        var repo = new Mock<IConfigRepository>();
        repo.Setup(r => r.ListConfigsWithDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var service = new ConfigUserValueService(repo.Object, Mock.Of<ICurrentUser>());

        await service.ApplyDefaultsForNewUserAsync(42, actorUserId: 0, CancellationToken.None);

        repo.Verify(r => r.AddUserValueAsync(It.IsAny<ConfigUser>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
