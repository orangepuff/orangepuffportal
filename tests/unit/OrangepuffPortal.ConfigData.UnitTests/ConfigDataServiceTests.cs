using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrangepuffPortal.ConfigData.Domain.Entity;
using OrangepuffPortal.ConfigData.Domain.Repositories;
using OrangepuffPortal.ConfigData.Infrastructure;
using System.Text.Json;

namespace OrangepuffPortal.ConfigData.UnitTests;

public class ConfigDataServiceTests
{
    private static ConfigDataCache NewCache(IDistributedCache? distributed = null) =>
        new(distributed ?? Mock.Of<IDistributedCache>(), NullLogger<ConfigDataCache>.Instance);

    private static byte[] Serialize(IReadOnlyList<ConfigDataCacheRow> rows) =>
        JsonSerializer.SerializeToUtf8Bytes(rows);

    [Fact]
    public async Task GetAsync_returns_value_from_cache_on_hit()
    {
        var rows = new List<ConfigDataCacheRow> { new(1, "app.name", "OCRWeb", true, null) };
        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Serialize(rows));

        var repo = new Mock<IConfigDataRepository>();
        var service = new ConfigDataService(NewCache(dist.Object), repo.Object);

        var result = await service.GetAsync("app.name");

        Assert.Equal("OCRWeb", result);
        repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_missing_key_when_cache_has_rows()
    {
        var rows = new List<ConfigDataCacheRow> { new(1, "other.key", "value", null, null) };
        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Serialize(rows));

        var service = new ConfigDataService(NewCache(dist.Object), Mock.Of<IConfigDataRepository>());

        var result = await service.GetAsync("app.name");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_falls_back_to_repository_on_cache_miss_and_populates_cache()
    {
        var entry = new ConfigDataEntry("app.name", "OCRWeb", true, null, DateTime.UtcNow);

        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entry]);

        var service = new ConfigDataService(NewCache(dist.Object), repo.Object);

        var result = await service.GetAsync("app.name");

        Assert.Equal("OCRWeb", result);
        repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        dist.Verify(d => d.SetAsync("ConfigData:all", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_is_case_insensitive_on_key()
    {
        var rows = new List<ConfigDataCacheRow> { new(1, "App.Name", "OCRWeb", null, null) };
        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Serialize(rows));

        var service = new ConfigDataService(NewCache(dist.Object), Mock.Of<IConfigDataRepository>());

        Assert.Equal("OCRWeb", await service.GetAsync("app.name"));
        Assert.Equal("OCRWeb", await service.GetAsync("APP.NAME"));
    }

    [Fact]
    public async Task GetAllAsync_returns_dtos_from_cache()
    {
        var rows = new List<ConfigDataCacheRow>
        {
            new(1, "key.a", "val-a", true, "desc-a"),
            new(2, "key.b", null, false, null),
        };
        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Serialize(rows));

        var service = new ConfigDataService(NewCache(dist.Object), Mock.Of<IConfigDataRepository>());

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("key.a", result[0].Key);
        Assert.Equal("val-a", result[0].Value);
        Assert.Equal(true, result[0].AllowEditByScreen);
        Assert.Equal("desc-a", result[0].Description);
        Assert.Null(result[1].Value);
    }

    [Fact]
    public async Task GetAllAsync_populates_cache_from_repository_on_miss()
    {
        var entry = new ConfigDataEntry("key.a", "val-a", null, null, DateTime.UtcNow);

        var dist = new Mock<IDistributedCache>();
        dist.Setup(d => d.GetAsync("ConfigData:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var repo = new Mock<IConfigDataRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([entry]);

        var service = new ConfigDataService(NewCache(dist.Object), repo.Object);

        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("key.a", result[0].Key);
        dist.Verify(d => d.SetAsync("ConfigData:all", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
