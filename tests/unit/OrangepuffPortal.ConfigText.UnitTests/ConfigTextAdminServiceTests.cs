using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.ConfigText.Contract;
using OrangepuffPortal.ConfigText.Domain.Entity;
using OrangepuffPortal.ConfigText.Domain.Repositories;
using OrangepuffPortal.ConfigText.Infrastructure;
using OrangepuffPortal.Shared.Paging;
using OrangepuffPortal.Shared.Translation;

namespace OrangepuffPortal.ConfigText.UnitTests;

public class ConfigTextAdminServiceTests
{
    private static ConfigTextCache NewCache() =>
        new(Mock.Of<IDistributedCache>(), NullLogger<ConfigTextCache>.Instance);

    private static ConfigTextAdminService NewService(
        IConfigTextRepository repo,
        ConfigTextCache? cache = null,
        ITranslation? translation = null) =>
        new(repo,
            cache ?? NewCache(),
            translation ?? Mock.Of<ITranslation>(),
            NullLogger<ConfigTextAdminService>.Instance);

    private static ConfigTextDefinition MakeEntry(string module = "Mod", string code = "title", string culture = "en-US", string type = "lbl", string text = "Hello") =>
        new(module, code, culture, type, text, null, DateTime.UtcNow);

    private static ConfigTextAdminUpsertRequest DefaultRequest(string culture = "en-US") =>
        new("Mod", "title", culture, "lbl", "Hello", null);

    private static void SetupTranslation(Mock<ITranslation> t, string code, string message) =>
        t.Setup(x => x.TranslateAsync(code, It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(message);

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_returns_paged_result()
    {
        var entry = MakeEntry();
        var pagedResult = new PagedResult<ConfigTextDefinition>([entry], 1);

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.ListAsync(null, null, null, null, null, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await NewService(repo.Object).ListAsync(null, null, null, null, null, page: 1, pageSize: 25);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Mod", result.Items[0].SModule);
        Assert.Equal("title", result.Items[0].STextCode);
        Assert.Equal("en-US", result.Items[0].SCultureCode);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_rejects_duplicate_key()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.ExistsAsync("Mod", "title", "en-US", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "duplicate_key", "Duplicate key.");

        var result = await NewService(repo.Object, translation: translation.Object).AddAsync(DefaultRequest(), actorUserId: 1);

        Assert.False(result.Success);
        Assert.Equal("Duplicate key.", result.RejectionReason);
        repo.Verify(r => r.AddAsync(It.IsAny<ConfigTextDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_creates_entry_and_wildcard_fallback_when_missing()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.ExistsAsync("Mod", "title", "en-US", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.ExistsAsync("Mod", "title", "*", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "configtext_created", "Created.");

        var result = await NewService(repo.Object, translation: translation.Object).AddAsync(DefaultRequest(), actorUserId: 1);

        Assert.True(result.Success);
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "en-US"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "*"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_skips_wildcard_backfill_when_wildcard_already_exists()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.ExistsAsync("Mod", "title", "en-US", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.ExistsAsync("Mod", "title", "*", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "configtext_created", "Created.");

        await NewService(repo.Object, translation: translation.Object).AddAsync(DefaultRequest(), actorUserId: 1);

        // Only the explicit "en-US" row is added — wildcard already present.
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "en-US"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "*"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_does_not_create_extra_wildcard_when_adding_wildcard_directly()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.ExistsAsync("Mod", "title", "*", "lbl", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "configtext_created", "Created.");

        await NewService(repo.Object, translation: translation.Object).AddAsync(DefaultRequest(culture: "*"), actorUserId: 1);

        // Exactly one AddAsync — the wildcard row itself; no additional backfill.
        repo.Verify(r => r.AddAsync(It.IsAny<ConfigTextDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_entry_missing()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigTextDefinition?)null);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "not_found", "Not found.");

        var result = await NewService(repo.Object, translation: translation.Object).UpdateAsync(99, DefaultRequest(), actorUserId: 1);

        Assert.False(result.Success);
        Assert.Equal("Not found.", result.RejectionReason);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_rejects_duplicate_key()
    {
        var entry = MakeEntry();
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        repo.Setup(r => r.ExistsAsync("Mod", "title", "en-US", "lbl", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "duplicate_key", "Duplicate key.");

        var result = await NewService(repo.Object, translation: translation.Object).UpdateAsync(1, DefaultRequest(), actorUserId: 1);

        Assert.False(result.Success);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_entry_saves_and_invalidates_cache()
    {
        var entry = MakeEntry(text: "Old text");
        var dist = new Mock<IDistributedCache>();
        var cache = new ConfigTextCache(dist.Object, NullLogger<ConfigTextCache>.Instance);

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        repo.Setup(r => r.ExistsAsync("Mod", "title", "en-US", "lbl", 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "configtext_updated", "Updated.");

        var result = await NewService(repo.Object, cache, translation.Object)
            .UpdateAsync(1, new ConfigTextAdminUpsertRequest("Mod", "title", "en-US", "lbl", "New text", null), actorUserId: 5);

        Assert.True(result.Success);
        Assert.Equal("New text", entry.Text);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_entry_missing()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetByIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync((ConfigTextDefinition?)null);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "not_found", "Not found.");

        var result = await NewService(repo.Object, translation: translation.Object).DeleteAsync(77);

        Assert.False(result.Success);
        repo.Verify(r => r.DeleteAsync(It.IsAny<ConfigTextDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_deletes_entry_saves_and_invalidates_cache()
    {
        var entry = MakeEntry();
        var dist = new Mock<IDistributedCache>();
        var cache = new ConfigTextCache(dist.Object, NullLogger<ConfigTextCache>.Instance);

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        var translation = new Mock<ITranslation>();
        SetupTranslation(translation, "configtext_deleted", "Deleted.");

        var result = await NewService(repo.Object, cache, translation.Object).DeleteAsync(8);

        Assert.True(result.Success);
        repo.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
