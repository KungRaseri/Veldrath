namespace Veldrath.Client.Rendering;

/// <summary>
/// Resolves the active <see cref="IMapRenderer"/> based on the current
/// <see cref="ClientSettings.RendererMode"/>. Registered as a singleton so the
/// underlying renderer instances (and their caches) survive control teardown,
/// but the <see cref="Current"/> property always reflects the active mode.
/// </summary>
public sealed class MapRendererResolver
{
    private readonly ClientSettings _settings;
    private readonly SpriteMapRenderer _sprite;
    private readonly AsciiMapRenderer _ascii;

    /// <summary>Initializes a new instance of <see cref="MapRendererResolver"/>.</summary>
    /// <param name="settings">Shared client settings whose <c>RendererMode</c> determines the active renderer.</param>
    /// <param name="sprite">The sprite-based map renderer.</param>
    /// <param name="ascii">The ASCII / text-based map renderer.</param>
    public MapRendererResolver(ClientSettings settings, SpriteMapRenderer sprite, AsciiMapRenderer ascii)
    {
        _settings = settings;
        _sprite = sprite;
        _ascii = ascii;
    }

    /// <summary>Gets the <see cref="IMapRenderer"/> that is currently active based on <see cref="ClientSettings.RendererMode"/>.</summary>
    public IMapRenderer Current => _settings.RendererMode switch
    {
        RendererMode.Ascii => _ascii,
        _                  => _sprite,
    };
}
