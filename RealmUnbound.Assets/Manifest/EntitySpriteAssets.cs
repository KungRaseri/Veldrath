namespace RealmUnbound.Assets.Manifest;

/// <summary>
/// Describes a single entity sprite sheet entry used by the client renderer.
/// Sprites follow the RPGMaker VX Ace walking-sheet convention: each character
/// block is <c>3 frames wide × 4 rows tall</c> (rows: S, W, E, N).
/// </summary>
/// <param name="Key">Unique sprite identifier matching <c>TileEntityDto.SpriteKey</c>.</param>
/// <param name="RelativePath">Path to the PNG relative to <c>AppContext.BaseDirectory</c>.</param>
/// <param name="SouthX">X pixel offset of the south-facing idle frame within the sheet.</param>
/// <param name="SouthY">Y pixel offset of the south-facing idle frame within the sheet.</param>
/// <param name="FrameWidth">Width in pixels of a single animation frame.</param>
/// <param name="FrameHeight">Height in pixels of a single animation frame.</param>
public record EntitySpriteInfo(
    string Key,
    string RelativePath,
    int SouthX,
    int SouthY,
    int FrameWidth,
    int FrameHeight)
{
    /// <summary>
    /// Returns the (x, y) pixel offset of the idle frame for the given <paramref name="direction"/>.
    /// Follows RPGMaker row order: S = row 0, W = row 1, E = row 2, N = row 3.
    /// Accepts both short-form cardinal letters (<c>"S"</c>) and long-form names (<c>"down"</c>).
    /// </summary>
    public (int X, int Y) GetFrameOffset(string direction) =>
        direction.ToUpperInvariant() switch
        {
            "S" or "SOUTH" or "DOWN"  => (SouthX, SouthY),
            "W" or "WEST"  or "LEFT"  => (SouthX, SouthY + FrameHeight),
            "E" or "EAST"  or "RIGHT" => (SouthX, SouthY + FrameHeight * 2),
            "N" or "NORTH" or "UP"    => (SouthX, SouthY + FrameHeight * 3),
            _                         => (SouthX, SouthY),
        };
}

/// <summary>
/// Compile-time catalog mapping entity sprite keys to their sheet metadata.
/// Keys match the <c>SpriteKey</c> field sent by the server in <c>ZoneEntitiesSnapshot</c>.
/// Relative paths are relative to <c>AppContext.BaseDirectory</c>.
/// </summary>
public static class EntitySpriteAssets
{
    // ── Character sheets ──────────────────────────────────────────────────────
    // chara2/3/npc1.png: RPGMaker VX Ace format, 78×144 px per character block,
    // 26×36 px per animation frame (3 cols × 4 rows within each block).
    // South-standing idle = middle frame of row 0 → blockX + 26, blockY + 0.

    /// <summary>Hero character — used for the local player and all remote players.</summary>
    public static readonly EntitySpriteInfo Player = new(
        Key:          "player",
        RelativePath: "GameAssets/entities/sheets/chara2.png",
        SouthX: 26, SouthY: 0,
        FrameWidth: 26, FrameHeight: 36);

    /// <summary>Ruffian humanoid — used for <c>bandit-ruffian</c> enemies.</summary>
    public static readonly EntitySpriteInfo BanditRuffian = new(
        Key:          "bandit-ruffian",
        RelativePath: "GameAssets/entities/sheets/chara3.png",
        SouthX: 26, SouthY: 0,
        FrameWidth: 26, FrameHeight: 36);

    /// <summary>Village blacksmith NPC — first character block in <c>npc1.png</c>.</summary>
    public static readonly EntitySpriteInfo VillageBlacksmith = new(
        Key:          "village-blacksmith",
        RelativePath: "GameAssets/entities/sheets/npc1.png",
        SouthX: 26, SouthY: 0,
        FrameWidth: 26, FrameHeight: 36);

    /// <summary>Tavern keeper NPC — second character block (column 1) in <c>npc1.png</c>.</summary>
    public static readonly EntitySpriteInfo TavernKeeper = new(
        Key:          "tavern-keeper",
        RelativePath: "GameAssets/entities/sheets/npc1.png",
        SouthX: 104, SouthY: 0,  // column 1: 1×78 + 26 = 104
        FrameWidth: 26, FrameHeight: 36);

    // ── Monster sheet ─────────────────────────────────────────────────────────
    // monster1.png (1× native): RPGMaker format, 144×256 px per monster block,
    // 48×64 px per animation frame (3 cols × 4 rows within each block).
    // South-standing idle = middle frame of row 0 → blockX + 48, blockY + 0.

    /// <summary>Goblin scout enemy — mapped to the first monster (green slime) in <c>monster1.png</c>.</summary>
    public static readonly EntitySpriteInfo GoblinScout = new(
        Key:          "goblin-scout",
        RelativePath: "GameAssets/entities/sheets/monster1.png",
        SouthX: 48, SouthY: 0,   // block col 0: 0×144 + 48
        FrameWidth: 48, FrameHeight: 64);

    /// <summary>All registered entity sprite infos, keyed by <see cref="EntitySpriteInfo.Key"/>.</summary>
    public static readonly IReadOnlyDictionary<string, EntitySpriteInfo> All =
        new Dictionary<string, EntitySpriteInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [Player.Key]            = Player,
            [BanditRuffian.Key]     = BanditRuffian,
            [VillageBlacksmith.Key] = VillageBlacksmith,
            [TavernKeeper.Key]      = TavernKeeper,
            [GoblinScout.Key]       = GoblinScout,
        };
}
