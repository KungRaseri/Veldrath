using Microsoft.Extensions.Logging.Abstractions;
using RealmForge.Models;
using RealmForge.Services;

namespace RealmForge.Tests.Services;

public class EditorSettingsServiceTests : IDisposable
{
    // Use a temp file so tests don't pollute real settings
    private readonly string _tempFile;
    private readonly EditorSettingsService _sut;

    public EditorSettingsServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"rf_test_{Guid.NewGuid():N}.json");
        // Inject via reflection to override the static path for testing
        _sut = CreateWithCustomPath(_tempFile);
    }

    // Helper: creates an EditorSettingsService pointing at a temp file
    private static EditorSettingsService CreateWithCustomPath(string path) =>
        new(NullLogger<EditorSettingsService>.Instance, path);

    [Fact]
    public async Task LoadSettingsAsync_Returns_DefaultSettings_When_No_File_Exists()
    {
        var settings = await _sut.LoadSettingsAsync();
        settings.Should().NotBeNull();
        settings.Theme.Should().Be("Dark");
    }

    [Fact]
    public async Task LoadSettingsAsync_Returns_CachedInstance_On_Second_Call()
    {
        var first = await _sut.LoadSettingsAsync();
        var second = await _sut.LoadSettingsAsync();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task UpdateSettingAsync_Mutates_And_Persists()
    {
        await _sut.UpdateSettingAsync(s => s.Theme = "Light");
        var settings = await _sut.LoadSettingsAsync();
        settings.Theme.Should().Be("Light");
    }

    [Fact]
    public async Task ResetToDefaultsAsync_Restores_Default_Theme()
    {
        await _sut.UpdateSettingAsync(s => s.Theme = "Light");
        await _sut.ResetToDefaultsAsync();
        var settings = await _sut.LoadSettingsAsync();
        settings.Theme.Should().Be("Dark");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
