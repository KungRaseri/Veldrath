using System.Diagnostics.CodeAnalysis;
using LibVLCSharp.Shared;

namespace RealmUnbound.Client.Services;

/// <summary>
/// LibVLC-backed <see cref="IAudioPlayer"/> implementation.
/// Supports OGG/MP3/WAV playback with indefinite music looping and fire-and-forget SFX.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class LibVlcAudioPlayer : IAudioPlayer
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _musicPlayer;
    private Media? _currentMusic;

    /// <summary>Initializes a new instance of <see cref="LibVlcAudioPlayer"/>.</summary>
    public LibVlcAudioPlayer()
    {
        Core.Initialize();
        _libVlc = new LibVLC("--no-video", "--quiet");
        _musicPlayer = new MediaPlayer(_libVlc);
        // Restart the track when it ends so music loops indefinitely.
        // Task.Run is required because VLC fires EndReached on its own internal thread
        // and calling Play() synchronously from that callback causes a deadlock.
        _musicPlayer.EndReached += (_, _) => Task.Run(() =>
        {
            _musicPlayer.Stop();
            _musicPlayer.Play();
        });
    }

    /// <inheritdoc />
    public Task PlayMusicAsync(string filePath)
    {
        _musicPlayer.Stop();
        _currentMusic?.Dispose();
        _currentMusic = new Media(_libVlc, filePath, FromType.FromPath);
        _musicPlayer.Media = _currentMusic;
        _musicPlayer.Play();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void PlaySfx(string filePath)
    {
        var sfxPlayer = new MediaPlayer(_libVlc);
        var sfxMedia  = new Media(_libVlc, filePath, FromType.FromPath);
        sfxPlayer.Media = sfxMedia;
        sfxMedia.Dispose();
        sfxPlayer.EndReached += (_, _) => sfxPlayer.Dispose();
        sfxPlayer.Play();
    }

    /// <inheritdoc />
    public void StopMusic() => _musicPlayer.Stop();

    /// <inheritdoc />
    public void Dispose()
    {
        _musicPlayer.Stop();
        _musicPlayer.Dispose();
        _currentMusic?.Dispose();
        _libVlc.Dispose();
    }
}
