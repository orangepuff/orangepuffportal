using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.ConfigText.Contract;
using OrangepuffPortal.ConfigText.Domain.Entity;
using OrangepuffPortal.ConfigText.Domain.Repositories;
using OrangepuffPortal.ConfigText.Infrastructure;

namespace OrangepuffPortal.ConfigText.UnitTests;

public class ConfigTextWriterTests
{
    private static ConfigTextCache NewCache() =>
        new(Mock.Of<IDistributedCache>(), NullLogger<ConfigTextCache>.Instance);

    private static ConfigTextWriter NewWriter(IConfigTextRepository repo, ConfigTextCache? cache = null) =>
        new(repo, cache ?? NewCache(), NullLogger<ConfigTextWriter>.Instance);

    private static ConfigTextDefinition MakeExisting(string module, string code, string culture, string type, string text) =>
        new(module, code, culture, type, text, null, DateTime.UtcNow);

    [Fact]
    public async Task UpsertManyAsync_inserts_both_culture_and_wildcard_rows_for_new_entry()
    {
        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.FindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigTextDefinition?)null);

        var entries = new[] { new ConfigTextSeedEntry("Mod", "title", "lbl", "Hello") };

        await NewWriter(repo.Object).UpsertManyAsync("en-US", entries);

        // Two AddAsync calls: one for "en-US", one for "*"
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "en-US"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == ConfigTextDefinition.WildcardCulture), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertManyAsync_skips_existing_entries_when_BtReplace_is_false()
    {
        var existing = MakeExisting("Mod", "title", "en-US", "lbl", "Old text");
        var existingWild = MakeExisting("Mod", "title", "*", "lbl", "Old text");

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.FindAsync("Mod", "title", "en-US", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repo.Setup(r => r.FindAsync("Mod", "title", "*", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync(existingWild);

        var entries = new[] { new ConfigTextSeedEntry("Mod", "title", "lbl", "New text", BtReplace: false) };

        await NewWriter(repo.Object).UpsertManyAsync("en-US", entries);

        repo.Verify(r => r.AddAsync(It.IsAny<ConfigTextDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Old text", existing.Text);
    }

    [Fact]
    public async Task UpsertManyAsync_replaces_existing_when_BtReplace_is_true()
    {
        var existing = MakeExisting("Mod", "title", "en-US", "lbl", "Old text");
        var existingWild = MakeExisting("Mod", "title", "*", "lbl", "Old text");

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.FindAsync("Mod", "title", "en-US", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repo.Setup(r => r.FindAsync("Mod", "title", "*", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync(existingWild);

        var entries = new[] { new ConfigTextSeedEntry("Mod", "title", "lbl", "New text", BtReplace: true) };

        await NewWriter(repo.Object).UpsertManyAsync("en-US", entries);

        repo.Verify(r => r.AddAsync(It.IsAny<ConfigTextDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("New text", existing.Text);
        Assert.Equal("New text", existingWild.Text);
    }

    [Fact]
    public async Task UpsertManyAsync_rejects_wildcard_as_culture_code()
    {
        var repo = new Mock<IConfigTextRepository>();
        var entries = new[] { new ConfigTextSeedEntry("Mod", "title", "lbl", "text") };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewWriter(repo.Object).UpsertManyAsync("*", entries));
    }

    [Fact]
    public async Task UpsertManyAsync_inserts_only_missing_culture_row_when_wildcard_already_exists()
    {
        var existingWild = MakeExisting("Mod", "title", "*", "lbl", "Existing wildcard");

        var repo = new Mock<IConfigTextRepository>();
        repo.Setup(r => r.FindAsync("Mod", "title", "en-US", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync((ConfigTextDefinition?)null);
        repo.Setup(r => r.FindAsync("Mod", "title", "*", "lbl", It.IsAny<CancellationToken>())).ReturnsAsync(existingWild);

        var entries = new[] { new ConfigTextSeedEntry("Mod", "title", "lbl", "New text") };

        await NewWriter(repo.Object).UpsertManyAsync("en-US", entries);

        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "en-US"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.Is<ConfigTextDefinition>(e => e.CultureCode == "*"), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
