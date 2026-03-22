using System.Diagnostics.CodeAnalysis;

namespace RealmUnbound.Client.Services;

/// <summary>No-op <see cref="IAudioPlayer"/> used when the native audio library is unavailable.</summary>
[ExcludeFromCodeCoverage]
internal sealed class NullAudioPlayer : IAudioPlayer
{
    /// <inheritdoc />
    public Task PlayMusicAsync(string filePath) => Task.CompletedTask;

    /// <inheritdoc />
    public void PlaySfx(string filePath) { }

    /// <inheritdoc />
    public void StopMusic() { }

    /// <inheritdoc />
    public void SetMusicVolume(int volume) { }

    /// <inheritdoc />
    public void SetSfxVolume(int volume) { }

    /// <inheritdoc />
    public void SetMuted(bool muted) { }

    /// <inheritdoc />
    public void Dispose() { }
}
