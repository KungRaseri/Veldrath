namespace Veldrath.Assets.Manifest;

/// <summary>
/// Path constants for audio assets from the Kenney game assets collection.
/// All paths are relative to the <c>GameAssets</c> root.
/// Pass to <see cref="IAssetStore.ResolveAudioPath"/> to obtain the full file path for streaming.
/// </summary>
public static class AudioAssets
{
    // RPG Ambient Effects
    /// <summary>Book open sound.</summary>
    public const string BookOpen = "audio/rpg/bookOpen.ogg";
    /// <summary>Book close sound.</summary>
    public const string BookClose = "audio/rpg/bookClose.ogg";
    /// <summary>Book page flip, variant 1.</summary>
    public const string BookFlip1 = "audio/rpg/bookFlip1.ogg";
    /// <summary>Book page flip, variant 2.</summary>
    public const string BookFlip2 = "audio/rpg/bookFlip2.ogg";
    /// <summary>Book page flip, variant 3.</summary>
    public const string BookFlip3 = "audio/rpg/bookFlip3.ogg";
    /// <summary>Book placed down sound.</summary>
    public const string BookPlace1 = "audio/rpg/bookPlace1.ogg";
    /// <summary>Door open, variant 1.</summary>
    public const string DoorOpen1 = "audio/rpg/doorOpen_1.ogg";
    /// <summary>Door open, variant 2.</summary>
    public const string DoorOpen2 = "audio/rpg/doorOpen_2.ogg";
    /// <summary>Door close, variant 1.</summary>
    public const string DoorClose1 = "audio/rpg/doorClose_1.ogg";
    /// <summary>Door close, variant 2.</summary>
    public const string DoorClose2 = "audio/rpg/doorClose_2.ogg";
    /// <summary>Coins handled sound.</summary>
    public const string HandleCoins = "audio/rpg/handleCoins.ogg";
    /// <summary>Coins handled sound, variant 2.</summary>
    public const string HandleCoins2 = "audio/rpg/handleCoins2.ogg";
    /// <summary>Knife drawn, variant 1.</summary>
    public const string DrawKnife1 = "audio/rpg/drawKnife1.ogg";
    /// <summary>Knife drawn, variant 2.</summary>
    public const string DrawKnife2 = "audio/rpg/drawKnife2.ogg";
    /// <summary>Knife slice, variant 1.</summary>
    public const string KnifeSlice1 = "audio/rpg/knifeSlice.ogg";
    /// <summary>Metal click sound.</summary>
    public const string MetalClick = "audio/rpg/metalClick.ogg";
    /// <summary>Metal latch sound.</summary>
    public const string MetalLatch = "audio/rpg/metalLatch.ogg";
    /// <summary>Wood chop sound.</summary>
    public const string Chop = "audio/rpg/chop.ogg";
    /// <summary>Footstep sound 00.</summary>
    public const string Footstep00 = "audio/rpg/footstep00.ogg";
    /// <summary>Footstep sound 01.</summary>
    public const string Footstep01 = "audio/rpg/footstep01.ogg";

    // UI Interface Sounds
    /// <summary>Button click, variant 1.</summary>
    public const string Click1 = "audio/interface/click_001.ogg";
    /// <summary>Button click, variant 2.</summary>
    public const string Click2 = "audio/interface/click_002.ogg";
    /// <summary>Confirmation sound, variant 1.</summary>
    public const string Confirm1 = "audio/interface/confirmation_001.ogg";
    /// <summary>Confirmation sound, variant 2.</summary>
    public const string Confirm2 = "audio/interface/confirmation_002.ogg";
    /// <summary>Error / failure sound, variant 1.</summary>
    public const string Error1 = "audio/interface/error_001.ogg";
    /// <summary>Error / failure sound, variant 2.</summary>
    public const string Error2 = "audio/interface/error_002.ogg";
    /// <summary>Item drop / place sound, variant 1.</summary>
    public const string Drop1 = "audio/interface/drop_001.ogg";
    /// <summary>Panel open sound, variant 1.</summary>
    public const string Open1 = "audio/interface/open_001.ogg";
    /// <summary>Panel close sound, variant 1.</summary>
    public const string Close1 = "audio/interface/close_001.ogg";
    /// <summary>Item select sound, variant 1.</summary>
    public const string Select1 = "audio/interface/select_001.ogg";
    /// <summary>Toggle switch sound, variant 1.</summary>
    public const string Toggle1 = "audio/interface/toggle_001.ogg";
    /// <summary>Scroll list sound, variant 1.</summary>
    public const string Scroll1 = "audio/interface/scroll_001.ogg";

    // Impact Sounds
    /// <summary>Heavy metal impact, variant 1.</summary>
    public const string ImpactMetalHeavy1 = "audio/impact/impactMetal_heavy_000.ogg";
    /// <summary>Medium metal impact, variant 1.</summary>
    public const string ImpactMetalMedium1 = "audio/impact/impactMetal_medium_000.ogg";
    /// <summary>Heavy punch impact, variant 1.</summary>
    public const string ImpactPunchHeavy1 = "audio/impact/impactPunch_heavy_000.ogg";
    /// <summary>Medium punch impact, variant 1.</summary>
    public const string ImpactPunchMedium1 = "audio/impact/impactPunch_medium_000.ogg";
    /// <summary>Heavy plate armour impact, variant 1.</summary>
    public const string ImpactPlateHeavy1 = "audio/impact/impactPlate_heavy_000.ogg";
    /// <summary>Heavy wood impact, variant 1.</summary>
    public const string ImpactWoodHeavy1 = "audio/impact/impactWood_heavy_000.ogg";
    /// <summary>Mining strike sound, variant 1.</summary>
    public const string ImpactMining1 = "audio/impact/impactMining_000.ogg";

    // Background Music
    /// <summary>Dungeon / cave ambient loop — "Infinite Descent".</summary>
    public const string MusicDungeon = "audio/music/Infinite Descent.ogg";
    /// <summary>Town melancholic loop — "Sad Town".</summary>
    public const string MusicTown = "audio/music/Sad Town.ogg";
    /// <summary>Flowing exploration loop — "Flowing Rocks".</summary>
    public const string MusicExplore = "audio/music/Flowing Rocks.ogg";
    /// <summary>Boss / dramatic loop — "Mission Plausible".</summary>
    public const string MusicBoss = "audio/music/Mission Plausible.ogg";
    /// <summary>Night / rest loop — "Night at the Beach".</summary>
    public const string MusicNight = "audio/music/Night at the Beach.ogg";
    /// <summary>Game over jingle — "Game Over".</summary>
    public const string MusicGameOver = "audio/music/Game Over.ogg";
}
