using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.ConfigText.Domain.Entity;
using OrangepuffPortal.ConfigText.Domain.Repositories;
using OrangepuffPortal.ConfigText.Infrastructure;

namespace OrangepuffPortal.ConfigText.UnitTests;

public class ConfigTextReaderTests
{
    // Always-miss cache — every GetAsync returns null so the factory is always invoked.
    private static ConfigTextCache NewMissCache()
    {
        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        return new ConfigTextCache(dist.Object, NullLogger<ConfigTextCache>.Instance);
    }

    private static ConfigTextDefinition MakeRow(string module, string code, string culture, string type, string text) =>
        new(module, code, culture, type, text, null, DateTime.UtcNow);

    private static ConfigTextReader NewReader(IConfigTextRepository repo, ConfigTextCache? cache = null) =>
        new(repo, cache ?? NewMissCache(), NullLogger<ConfigTextReader>.Instance);

    [Fact]
    public async Task GetAllAsync_returns_all_entries_when_no_module_filter()
    {
        var rows = new[]
        {
            MakeRow("ModA", "title", "en-US", "lbl", "Title A"),
            MakeRow("ModB", "title", "en-US", "lbl", "Title B"),
        };

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetForCultureAsync("en-US", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await NewReader(repo.Object).GetAllAsync("en-US");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_filters_to_requested_module()
    {
        var rows = new[]
        {
            MakeRow("ModA", "title", "en-US", "lbl", "Title A"),
            MakeRow("ModB", "title", "en-US", "lbl", "Title B"),
        };

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetForCultureAsync("en-US", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await NewReader(repo.Object).GetAllAsync("en-US", modules: ["ModA"]);

        Assert.Single(result);
        Assert.Equal("ModA", result[0].SModule);
        Assert.Equal("Title A", result[0].SText);
    }

    [Fact]
    public async Task GetAllAsync_prefers_exact_culture_over_wildcard()
    {
        // Both "en-US" and "*" exist for the same key — reader must pick "en-US".
        var rows = new[]
        {
            MakeRow("Mod", "greeting", "*", "msg", "Hello (fallback)"),
            MakeRow("Mod", "greeting", "en-US", "msg", "Hello (en-US)"),
        };

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetForCultureAsync("en-US", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await NewReader(repo.Object).GetAllAsync("en-US");

        Assert.Single(result);
        Assert.Equal("Hello (en-US)", result[0].SText);
    }

    [Fact]
    public async Task GetAllAsync_falls_back_to_wildcard_when_exact_culture_missing()
    {
        var rows = new[]
        {
            MakeRow("Mod", "greeting", "*", "msg", "Hello (fallback)"),
        };

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetForCultureAsync("fr-FR", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await NewReader(repo.Object).GetAllAsync("fr-FR");

        Assert.Single(result);
        Assert.Equal("Hello (fallback)", result[0].SText);
    }

    [Fact]
    public async Task GetAllAsync_deduplicates_when_same_key_has_multiple_culture_rows()
    {
        // Only one result per (module, code, type) — the one with the exact culture wins.
        var rows = new[]
        {
            MakeRow("Mod", "key", "*", "lbl", "Fallback"),
            MakeRow("Mod", "key", "en-US", "lbl", "English"),
        };

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.GetForCultureAsync("en-US", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await NewReader(repo.Object).GetAllAsync("en-US");

        Assert.Single(result);
        Assert.Equal("English", result[0].SText);
    }
}
