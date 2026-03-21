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
}
