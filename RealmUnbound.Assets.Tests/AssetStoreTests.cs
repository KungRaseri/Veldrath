using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Veldrath.Assets;
using Veldrath.Assets.Manifest;

namespace Veldrath.Assets.Tests;

public sealed class AssetStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AssetStore _sut;

    public AssetStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"AssetStoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Populate a minimal GameAssets structure
        CreateAsset("enemies/goblin_01.png", [0x89, 0x50, 0x4E, 0x47]); // PNG header
        CreateAsset("items/weapons/Weapon_01.png", [0x89, 0x50, 0x4E, 0x47]);
        CreateAsset("audio/rpg/bookOpen.ogg", [0x4F, 0x67, 0x67, 0x53]); // OGG header
        CreateAsset("classes/Badge_warrior.png", [0x89, 0x50, 0x4E, 0x47]);

        var options = Options.Create(new AssetStoreOptions { BasePath = _tempDir });
        var cache = new ServiceCollection()
            .AddMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IMemoryCache>();

        _sut = new AssetStore(options, cache);
    }

    [Fact]
    public async Task LoadImageAsync_ReturnsBytes_WhenAssetExists()
    {
        var result = await _sut.LoadImageAsync(EnemyAssets.Goblin1);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadImageAsync_ReturnsNull_WhenAssetMissing()
    {
        var result = await _sut.LoadImageAsync("enemies/does_not_exist.png");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadImageAsync_ReturnsCachedResult_OnSubsequentCalls()
    {
        var first  = await _sut.LoadImageAsync(EnemyAssets.Goblin1);
        var second = await _sut.LoadImageAsync(EnemyAssets.Goblin1);

        second.Should().BeSameAs(first, "the same array instance should be returned from cache");
    }

    [Fact]
    public void ResolveAudioPath_ReturnsAbsolutePath_WhenAudioExists()
    {
        var result = _sut.ResolveAudioPath(AudioAssets.BookOpen);

        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("bookOpen.ogg");
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public void ResolveAudioPath_ReturnsNull_WhenAudioMissing()
    {
        var result = _sut.ResolveAudioPath("audio/rpg/missing.ogg");

        result.Should().BeNull();
    }

    [Fact]
    public void GetPaths_ReturnsRelativePaths_ForPopulatedCategory()
    {
        var paths = _sut.GetPaths(AssetCategory.Enemies).ToList();

        paths.Should().ContainSingle();
        paths[0].Should().Be("enemies/goblin_01.png");
    }

    [Fact]
    public void GetPaths_ReturnsEmpty_WhenCategoryDirectoryAbsent()
    {
        var paths = _sut.GetPaths(AssetCategory.Spells);

        paths.Should().BeEmpty();
    }

    [Fact]
    public void Exists_ReturnsTrue_WhenAssetOnDisk()
    {
        _sut.Exists(EnemyAssets.Goblin1).Should().BeTrue();
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenAssetAbsent()
    {
        _sut.Exists("enemies/missing.png").Should().BeFalse();
    }

    [Fact]
    public void SpellAssets_FolderForTradition_MapsCorrectly()
    {
        SpellAssets.FolderForTradition("Arcane").Should().Be("violet");
        SpellAssets.FolderForTradition("Divine").Should().Be("yellow");
        SpellAssets.FolderForTradition("Primal").Should().Be("green");
        SpellAssets.FolderForTradition("Occult").Should().Be("red");
        SpellAssets.FolderForTradition("Unknown").Should().Be("blue");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateAsset(string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(_tempDir, "GameAssets",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
    }
}
