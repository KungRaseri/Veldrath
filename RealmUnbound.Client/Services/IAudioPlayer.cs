namespace RealmUnbound.Client.Services;

/// <summary>Provides game audio playback for background music loops and one-shot sound effects.</summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>
    /// Starts playing a music track, looping indefinitely.
    /// Stops any currently playing music before starting the new track.
    /// </summary>
    /// <param name="filePath">Absolute file-system path to the audio file.</param>
    /// <returns>A task that completes once playback has been initiated.</returns>
    Task PlayMusicAsync(string filePath);

    /// <summary>
    /// Plays a one-shot sound effect without interrupting the currently playing music.
    /// </summary>
    /// <param name="filePath">Absolute file-system path to the audio file.</param>
    void PlaySfx(string filePath);

    /// <summary>Stops the currently playing music track.</summary>
    void StopMusic();

    /// <summary>Sets the background music volume (0–100). Does not affect SFX.</summary>
    /// <param name="volume">Volume level from 0 (silent) to 100 (full).</param>
    void SetMusicVolume(int volume);

    /// <summary>Sets the sound effect volume (0–100). Does not affect music.</summary>
    /// <param name="volume">Volume level from 0 (silent) to 100 (full).</param>
    void SetSfxVolume(int volume);

    /// <summary>Mutes or unmutes all audio output without changing the stored volume levels.</summary>
    /// <param name="muted"><see langword="true"/> to silence all output; <see langword="false"/> to restore.</param>
    void SetMuted(bool muted);
}
