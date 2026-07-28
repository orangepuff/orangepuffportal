using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.ConfigData.Contract;
using OrangepuffPortal.ConfigData.Domain.Entity;
using OrangepuffPortal.ConfigData.Domain.Repositories;
using OrangepuffPortal.ConfigData.Infrastructure;
using OrangepuffPortal.Shared.Translation;

namespace OrangepuffPortal.ConfigData.UnitTests;

public class ConfigDataAdminServiceTests
{
    private static ConfigDataAdminService NewService(
        IConfigDataRepository repo,
        ConfigDataCache? cache = null,
        ITranslation? translation = null) =>
        new(repo,
            cache ?? new ConfigDataCache(Mock.Of<IDistributedCache>(), NullLogger<ConfigDataCache>.Instance),
            translation ?? Mock.Of<ITranslation>(),
            NullLogger<ConfigDataAdminService>.Instance);

    private static ConfigDataEntry MakeEntry(int id = 1, string key = "some.key") =>
        new(key, "val", true, null, DateTime.UtcNow);

    // ── GetAllAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_returns_all_entries_as_dtos()
    {
        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ConfigDataEntry("key.a", "v", true, "desc", DateTime.UtcNow)]);

        var result = await NewService(repo.Object).GetAllAsync();

        Assert.Single(result);
        Assert.Equal("key.a", result[0].Key);
        Assert.Equal("v", result[0].Value);
        Assert.Equal("desc", result[0].Description);
    }

    // ── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_rejects_duplicate_key()
    {
        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.ExistsAsync("dup.key", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("duplicate_key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Duplicate key.");

        var result = await NewService(repo.Object, translation: translation.Object)
            .CreateAsync(new ConfigDataUpsertRequest("dup.key", null, null, null), actorUserId: 1);

        Assert.False(result.Success);
        Assert.Equal("Duplicate key.", result.RejectionReason);
        repo.Verify(r => r.AddAsync(It.IsAny<ConfigDataEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_creates_entry_saves_and_invalidates_cache()
    {
        var dist = new Mock<IDistributedCache>();
        var cache = new ConfigDataCache(dist.Object, NullLogger<ConfigDataCache>.Instance);

        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.ExistsAsync("new.key", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("configdata_created", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Created.");

        var result = await NewService(repo.Object, cache, translation.Object)
            .CreateAsync(new ConfigDataUpsertRequest("new.key", "value", true, "desc"), actorUserId: 99);

        Assert.True(result.Success);
        Assert.Equal("Created.", result.SuccessMessage);
        repo.Verify(r => r.AddAsync(It.Is<ConfigDataEntry>(e => e.Key == "new.key" && e.Value == "value"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dist.Verify(d => d.RemoveAsync("ConfigData:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_entry_missing()
    {
        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigDataEntry?)null);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("not_found", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Not found.");

        var result = await NewService(repo.Object, translation: translation.Object)
            .UpdateAsync(99, new ConfigDataUpsertRequest("k", null, null, null), actorUserId: 1);

        Assert.False(result.Success);
        Assert.Equal("Not found.", result.RejectionReason);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_rejects_when_key_already_used_by_another_entry()
    {
        var entry = MakeEntry(id: 1, key: "original.key");
        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        repo.Setup(r => r.ExistsAsync("dup.key", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("duplicate_key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Duplicate key.");

        var result = await NewService(repo.Object, translation: translation.Object)
            .UpdateAsync(1, new ConfigDataUpsertRequest("dup.key", null, null, null), actorUserId: 1);

        Assert.False(result.Success);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_entry_saves_and_invalidates_cache()
    {
        var entry = MakeEntry(id: 5, key: "old.key");
        var dist = new Mock<IDistributedCache>();
        var cache = new ConfigDataCache(dist.Object, NullLogger<ConfigDataCache>.Instance);

        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        repo.Setup(r => r.ExistsAsync("new.key", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("configdata_updated", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Updated.");

        var result = await NewService(repo.Object, cache, translation.Object)
            .UpdateAsync(5, new ConfigDataUpsertRequest("new.key", "new-val", false, "new desc"), actorUserId: 7);

        Assert.True(result.Success);
        Assert.Equal(5, result.Id);
        Assert.Equal("new.key", entry.Key);
        Assert.Equal("new-val", entry.Value);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dist.Verify(d => d.RemoveAsync("ConfigData:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_entry_missing()
    {
        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigDataEntry?)null);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("not_found", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Not found.");

        var result = await NewService(repo.Object, translation: translation.Object).DeleteAsync(42);

        Assert.False(result.Success);
        repo.Verify(r => r.DeleteAsync(It.IsAny<ConfigDataEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_deletes_entry_saves_and_invalidates_cache()
    {
        var entry = MakeEntry(id: 3);
        var dist = new Mock<IDistributedCache>();
        var cache = new ConfigDataCache(dist.Object, NullLogger<ConfigDataCache>.Instance);

        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        var translation = new Mock<ITranslation>();
        translation.Setup(t => t.TranslateAsync("configdata_deleted", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Deleted.");

        var result = await NewService(repo.Object, cache, translation.Object).DeleteAsync(3);

        Assert.True(result.Success);
        Assert.Equal("Deleted.", result.SuccessMessage);
        repo.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dist.Verify(d => d.RemoveAsync("ConfigData:all", It.IsAny<CancellationToken>()), Times.Once);
    }
}
