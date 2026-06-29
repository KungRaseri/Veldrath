# ASCII Tile & Entity System — Comprehensive Design

## 1. Goals

1. **Unified tile identity** — replace the two independent `GetChar`/`GetColor` switch expressions in [`TileAsciiMap.cs`](Veldrath.Client/Rendering/TileAsciiMap.cs) with a single `TileDescriptor` record that bundles all rendering properties for a tile.
2. **Configurable entity appearance** — replace the hardcoded `"player"→@, _→E` switch in [`AsciiMapRenderer.cs`](Veldrath.Client/Rendering/AsciiMapRenderer.cs:155) with an entity appearance registry keyed by `SpriteKey`.
3. **Named color palette** — extract all raw `Color.FromRgb()` literals into a named `AsciiPalette` class, enabling theme swaps.
4. **Per-tile foreground coloring** — change from per-row `FormattedText` to per-tile `FormattedText` so the existing `GetColor` data actually renders.
5. **Reusability** — the tile descriptors and entity appearances are consumable by both `SpriteMapRenderer` (for metadata/tooltips) and `AsciiMapRenderer` (for visual rendering).
6. **Zero engine changes** — all new types live in `Veldrath.Client.Rendering`. The engine's `TileIndex` constants remain pure integer references.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Veldrath.Client.Rendering (new types)                      │
│                                                             │
│  ┌──────────────┐  ┌───────────────────┐  ┌──────────────┐ │
│  │ AsciiPalette │  │ TileRegistry      │  │ EntityAppear- │ │
│  │ (named       │  │ (tile index →     │  │ anceRegistry  │ │
│  │  color slots)│  │  TileDescriptor)  │  │ (SpriteKey→   │ │
│  └──────┬───────┘  └────────┬──────────┘  │  appearance)  │ │
│         │                   │              └───────┬──────┘ │
│         │    ┌──────────────┘                      │        │
│         │    │                                     │        │
│         ▼    ▼                                     ▼        │
│  ┌──────────────────┐                   ┌──────────────────┐│
│  │ AsciiMapRenderer │                   │ SpriteMapRenderer││
│  │ (uses chars,     │                   │ (uses metadata   ││
│  │  colors, pal)    │                   │  for tooltips)   ││
│  └──────────────────┘                   └──────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

All new types coexist in the existing `Veldrath.Client.Rendering` namespace. No new project or assembly.

---

## 3. New Types

### 3.1 `TileCategory` — Enum

**File:** `Veldrath.Client/Rendering/TileCategory.cs`

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>Broad classification of a tile for rendering and gameplay purposes.</summary>
public enum TileCategory
{
    /// <summary>Unknown or unmapped tile index.</summary>
    Unknown = 0,
    /// <summary>Transparent / empty cell (-1).</summary>
    Empty,
    /// <summary>Ground texture overlay (dead leaves, gravel, cobblestone, foliage).</summary>
    Ground,
    /// <summary>Solid terrain fill (stone wall, sand).</summary>
    Terrain,
    /// <summary>Water tiles (deep water).</summary>
    Water,
    /// <summary>Flora — trees, cacti, ground cover, boulders, mushrooms.</summary>
    Flora,
    /// <summary>Dirt path / road tiles.</summary>
    Path,
    /// <summary>Special / system tiles (blank, pending).</summary>
    Special,
}
```

### 3.2 `TileDescriptor` — Record Struct

**File:** `Veldrath.Client/Rendering/TileDescriptor.cs`

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>
/// Complete rendering descriptor for a single tile index.
/// Bundles ASCII character, colours, and semantic metadata into one lookup result.
/// Both <see cref="AsciiMapRenderer"/> and <see cref="SpriteMapRenderer"/> can consume this.
/// </summary>
/// <param name="TileIndex">The spritesheet tile index (matches <see cref="RealmEngine.Shared.Models.TileIndex"/> constants).</param>
/// <param name="Name">Human-readable tile name (e.g. "Stone Wall", "Deep Water", "Pine Tree").</param>
/// <param name="Category">Broad tile classification.</param>
/// <param name="AsciiChar">Character to display in ASCII mode. Use <c>' '</c> for transparent/empty.</param>
/// <param name="Foreground">Foreground text colour in ASCII mode.</param>
/// <param name="Background">Optional background cell colour in ASCII mode. <see langword="null"/> means transparent.</param>
public readonly record struct TileDescriptor(
    int TileIndex,
    string Name,
    TileCategory Category,
    char AsciiChar,
    Color Foreground,
    Color? Background);
```

Construction follows the "positional record + constructor overload" pattern used elsewhere in the codebase. A convenience constructor accepts `(int tileIndex, string name, TileCategory category, char asciiChar, Color foreground)` with `Background = null`.

### 3.3 `TileRegistry` — Static Lookup

**File:** `Veldrath.Client/Rendering/TileRegistry.cs`

Replaces [`TileAsciiMap`](Veldrath.Client/Rendering/TileAsciiMap.cs) entirely (old file deleted).

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>
/// Registry mapping tile indices to <see cref="TileDescriptor"/> values.
/// Populated at static-init time from the <see cref="RealmEngine.Shared.Models.TileIndex"/> constants.
/// Both renderers use this as the single source of truth for tile identity.
/// </summary>
public static class TileRegistry
{
    private static readonly Dictionary<int, TileDescriptor> _descriptors = [];
    private static readonly TileDescriptor UnknownDescriptor = new(-999, "Unknown", TileCategory.Unknown, '?', AsciiPalette.DebugUnknown, null);

    static TileRegistry()
    {
        // Populated inline — see Section 4 for full mapping table
        RegisterSpecial();
        RegisterGround();
        RegisterTerrain();
        RegisterWater();
        RegisterFlora();
        RegisterPaths();
    }

    /// <summary>Looks up the descriptor for a tile index. Returns <c>UnknownDescriptor</c> for unmapped indices.</summary>
    public static TileDescriptor Get(int tileIndex) =>
        _descriptors.TryGetValue(tileIndex, out var d) ? d : UnknownDescriptor;

    private static void Register(int tileIndex, string name, TileCategory category, char asciiChar, Color foreground, Color? background = null) =>
        _descriptors[tileIndex] = new TileDescriptor(tileIndex, name, category, asciiChar, foreground, background);

    // Registration methods per category (see Section 4 for values)...
}
```

### 3.4 `AsciiPalette` — Named Color Slots

**File:** `Veldrath.Client/Rendering/AsciiPalette.cs`

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>
/// Named palette of <see cref="Color"/> and <see cref="IBrush"/> slots used by the ASCII renderer.
/// Extracted from hardcoded literals in <see cref="AsciiMapRenderer"/> and <see cref="TileAsciiMap"/>.
/// Replaceable for theming (dark mode, high-contrast, colourblind palettes).
/// </summary>
public static class AsciiPalette
{
    // ── Tile colours ─────────────────────────────────────────────────
    public static Color GroundDeadLeaves   => Color.FromRgb(74, 222, 128);
    public static Color GroundGravel       => Color.FromRgb(148, 163, 184);
    public static Color GroundCobblestone  => Color.FromRgb(148, 163, 184);
    public static Color GroundStoneTile    => Color.FromRgb(148, 163, 184);
    public static Color GroundLightFoliage => Color.FromRgb(34, 197, 94);
    public static Color GroundMedFoliage   => Color.FromRgb(34, 197, 94);
    public static Color GroundGrassFill    => Color.FromRgb(74, 222, 128);
    public static Color TerrainStone       => Color.FromRgb(148, 163, 184);
    public static Color TerrainSand        => Color.FromRgb(251, 191, 36);
    public static Color WaterDeep          => Color.FromRgb(96, 165, 250);
    public static Color FloraTree          => Color.FromRgb(34, 197, 94);
    public static Color FloraCactus        => Color.FromRgb(34, 197, 94);
    public static Color FloraGroundCover   => Color.FromRgb(34, 197, 94);
    public static Color FloraDead          => Color.FromRgb(148, 163, 184);
    public static Color FloraBoulder       => Color.FromRgb(148, 163, 184);
    public static Color FloraMushroom      => Color.FromRgb(34, 197, 94);
    public static Color PathTan            => Color.FromRgb(212, 165, 116);
    public static Color Blank              => Color.FromRgb(74, 222, 128);
    public static Color DebugUnknown       => Color.FromRgb(255, 0, 255);

    // ── Entity colours ───────────────────────────────────────────────
    public static Color PlayerSelf      => Color.FromRgb(6, 182, 212);
    public static Color PlayerOther     => Color.FromRgb(74, 222, 128);
    public static Color Enemy           => Color.FromRgb(239, 68, 68);
    public static Color Npc             => Color.FromRgb(250, 204, 21);

    // ── UI / overlay colours ─────────────────────────────────────────
    public static Color ExitHighlight       => Color.FromRgb(250, 204, 21);
    public static Color ZoneEntryHighlight  => Color.FromRgb(34, 197, 94);
    public static Color RegionExitHighlight => Color.FromRgb(249, 115, 22);
    public static Color LabelText           => Color.FromRgb(203, 213, 225);
    public static Color FogOverlay          => Color.FromRgb(30, 41, 59);
    public static Color DefaultTileText     => Color.FromRgb(148, 163, 184);

    // ── Minimap colours ──────────────────────────────────────────────
    public static Color MiniBg           => Color.FromArgb(210, 8, 10, 20);
    public static Color MiniWall         => Color.FromRgb(30, 30, 40);
    public static Color MiniFloor        => Color.FromRgb(80, 80, 100);
    public static Color MiniSelfDot      => Color.FromRgb(6, 182, 212);
    public static Color MiniOtherDot     => Color.FromRgb(30, 144, 255);

    // ── Brushes (cached for performance — Avalonia thread safety) ────
    public static IBrush PlayerSelfBrush      { get; } = new SolidColorBrush(PlayerSelf);
    public static IBrush PlayerOtherBrush     { get; } = new SolidColorBrush(PlayerOther);
    public static IBrush EnemyBrush           { get; } = new SolidColorBrush(Enemy);
    public static IBrush NpcBrush             { get; } = new SolidColorBrush(Npc);
    public static IBrush ExitHighlightBrush       { get; } = new SolidColorBrush(ExitHighlight);
    public static IBrush ZoneEntryHighlightBrush  { get; } = new SolidColorBrush(ZoneEntryHighlight);
    public static IBrush RegionExitHighlightBrush { get; } = new SolidColorBrush(RegionExitHighlight);
    public static IBrush LabelBrush               { get; } = new SolidColorBrush(LabelText);
    public static IBrush FogBrush                 { get; } = new SolidColorBrush(FogOverlay);
}
```

**Note on thread safety:** `static readonly IBrush` properties are safe because Avalonia's `SolidColorBrush` is immutable once constructed. The `{}` getter form is used (not `=>`) to ensure each property is a distinct brush instance, avoiding accidental mutation.

### 3.5 `EntityAppearance` — Record Struct

**File:** `Veldrath.Client/Rendering/EntityAppearance.cs`

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>
/// Rendering appearance for an entity type in ASCII mode.
/// Maps a <see cref="RenderEntity.SpriteKey"/> or <see cref="RenderEntity.EntityType"/> to visual properties.
/// </summary>
/// <param name="Character">Display character in ASCII mode.</param>
/// <param name="Color">Foreground colour for the character.</param>
public readonly record struct EntityAppearance(char Character, Color Color);
```

### 3.6 `EntityAppearanceRegistry` — Static Lookup

**File:** `Veldrath.Client/Rendering/EntityAppearanceRegistry.cs`

```csharp
namespace Veldrath.Client.Rendering;

/// <summary>
/// Maps entity sprite keys (and entity-type fallbacks) to their ASCII rendering appearance.
/// The <see cref="SpriteMapRenderer"/> doesn't use this for visuals but could use it for tooltips/labels.
/// </summary>
public static class EntityAppearanceRegistry
{
    private static readonly Dictionary<string, EntityAppearance> _byKey = new(StringComparer.OrdinalIgnoreCase);

    static EntityAppearanceRegistry()
    {
        // Defaults
        Register("player",          '@', AsciiPalette.PlayerSelf);
        Register("enemy",           'E', AsciiPalette.Enemy);
        Register("npc",             'N', AsciiPalette.Npc);

        // Common enemies — sprite keys match EntitySpriteAssets.All
        Register("goblin-scout",    'g', AsciiPalette.Enemy);
        Register("goblin-warrior",  'G', AsciiPalette.Enemy);
        Register("skeleton",        'S', AsciiPalette.Enemy);
        Register("skeleton-archer", 'S', AsciiPalette.Enemy);
        Register("wolf",            'w', AsciiPalette.Enemy);
        Register("bear",            'B', AsciiPalette.Enemy);
        Register("giant-spider",    's', AsciiPalette.Enemy);
        Register("slime",           'o', AsciiPalette.Enemy);
        Register("bat",             'b', AsciiPalette.Enemy);
        Register("dragon",          'D', AsciiPalette.Enemy);
        Register("boss",            'M', AsciiPalette.Enemy);  // "M" for "monster" / mini-boss
    }

    /// <summary>
    /// Looks up the appearance for a given entity. Prefers an exact <paramref name="spriteKey"/> match,
    /// falling back to <paramref name="entityType"/> (<c>"player"</c>, <c>"enemy"</c>, <c>"npc"</c>).
    /// </summary>
    public static EntityAppearance Get(string? spriteKey, string entityType)
    {
        if (spriteKey is not null && _byKey.TryGetValue(spriteKey, out var bySprite))
            return bySprite;

        return _byKey.TryGetValue(entityType, out var byType)
            ? byType
            : new EntityAppearance('?', AsciiPalette.DebugUnknown);
    }

    /// <summary>Registers an entity appearance, keyed by sprite key or entity type.</summary>
    public static void Register(string key, char character, Color color) =>
        _byKey[key] = new EntityAppearance(character, color);
}
```

---

## 4. Tile Mapping Table

Complete mapping from `TileIndex` constants → `TileDescriptor`. The `TileRegistry` static constructor populates these.

### 4.1 Special

| TileIndex Constant | Name | Category | Char | Foreground |
|---|---|---|---|---|
| `-1` (transparent) | "Empty" | Empty | `' '` | *n/a* |
| `TileIndex.Blank` (0) | "Blank" | Special | `'.'` | `Palette.Blank` |
| `TileIndex.Pending` (-2) | "Pending" | Special | `'?'` | `Palette.DebugUnknown` |

### 4.2 Ground Textures

| TileIndex Constant | Name | Category | Char | Foreground |
|---|---|---|---|---|
| `Ground.DeadLeaves` (1) | "Dead Leaves" | Ground | `','` | `Palette.GroundDeadLeaves` |
| `Ground.LightGravel` (2) | "Light Gravel" | Ground | `':'` | `Palette.GroundGravel` |
| `Ground.Cobblestone` (3) | "Cobblestone" | Ground | `';'` | `Palette.GroundCobblestone` |
| `Ground.StoneTile` (4) | "Stone Tile" | Ground | `'='` | `Palette.GroundStoneTile` |
| `Ground.LightFoliage` (5) | "Light Foliage" | Ground | `'"'` | `Palette.GroundLightFoliage` |
| `Ground.MedFoliage` (6) | "Medium Foliage" | Ground | `'\''` | `Palette.GroundMedFoliage` |
| `Ground.GrassFill` (7) | "Grass Fill" | Ground | `'"'` | `Palette.GroundGrassFill` |

### 4.3 Terrain

| TileIndex Constant | Name | Category | Char | Foreground | Background |
|---|---|---|---|---|---|
| `Terrain.Stone.M` (202) | "Stone Wall" | Terrain | `'#'` | `Palette.TerrainStone` | `Color.FromRgb(40,40,50)` |
| `Terrain.Sand.M` (445) | "Sand" | Terrain | `'~'` | `Palette.TerrainSand` | `Color.FromRgb(60,50,30)` |

### 4.4 Water

| TileIndex Constant | Name | Category | Char | Foreground | Background |
|---|---|---|---|---|---|
| `Water.Deep` (207) | "Deep Water" | Water | `'≈'` | `Palette.WaterDeep` | `Color.FromRgb(10,30,60)` |

### 4.5 Flora

| TileIndex Constant | Name | Category | Char | Foreground |
|---|---|---|---|---|
| `Flora.TreeA` (49) | "Sparse Tree" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.TreeB` (50) | "Tree B" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.Pine` (51) | "Pine Tree" | Flora | `'▲'` | `Palette.FloraTree` |
| `Flora.TreeC` (52) | "Tree C" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.TreeD` (53) | "Tree D" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.TreeE` (54) | "Dense Tree" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.Cactus` (55) | "Cactus" | Flora | `'Y'` | `Palette.FloraCactus` |
| `Flora.CactusDual` (56) | "Dual Cactus" | Flora | `'Y'` | `Palette.FloraCactus` |
| `Flora.TallGrass` (98) | "Tall Grass" | Flora | `'i'` | `Palette.FloraGroundCover` |
| `Flora.Vines` (99) | "Vines" | Flora | `'v'` | `Palette.FloraGroundCover` |
| `Flora.ClimbingVine` (100) | "Climbing Vine" | Flora | `'v'` | `Palette.FloraGroundCover` |
| `Flora.DualPine` (101) | "Dual Pine" | Flora | `'▲'` | `Palette.FloraTree` |
| `Flora.BigTree` (102) | "Big Tree" | Flora | `'T'` | `Palette.FloraTree` |
| `Flora.Boulder` (103) | "Boulder" | Flora | `'O'` | `Palette.FloraBoulder` |
| `Flora.DeadVines` (104) | "Dead Vines" | Flora | `'v'` | `Palette.FloraDead` |
| `Flora.Mushroom` (105) | "Mushroom" | Flora | `'*'` | `Palette.FloraMushroom` |

### 4.6 Dirt Paths

All path tiles use `'═'` `'║'` `'╔'` `'╗'` `'╚'` `'╝'` `'╠'` `'╣'` `'╦'` `'╩'` `'╬'` (Unicode box-drawing characters) instead of the previous `'+'` `'-'` `'|'` approximations.

| TileIndex Constant | Name | Char | Foreground |
|---|---|---|---|
| `DirtPath.FourWay` (444) | "Path Crossroads" | `'╬'` | `Palette.PathTan` |
| `DirtPath.Circle` (191) | "Path Circle" | `'o'` | `Palette.PathTan` |
| `DirtPath.StraightH` (192) | "Path Horizontal" | `'═'` | `Palette.PathTan` |
| `DirtPath.StraightV` (343) | "Path Vertical" | `'║'` | `Palette.PathTan` |
| `DirtPath.CornerTL` (292) | "Path Corner NW" | `'╝'` | `Palette.PathTan` |
| `DirtPath.CornerTR` (348) | "Path Corner NE" | `'╚'` | `Palette.PathTan` |
| `DirtPath.CornerBL` (184) | "Path Corner SW" | `'╗'` | `Palette.PathTan` |
| `DirtPath.CornerBR` (260) | "Path Corner SE" | `'╔'` | `Palette.PathTan` |
| `DirtPath.TJunctionLBR` (261) | "Path T-Junction N" | `'╩'` | `Palette.PathTan` |
| `DirtPath.TJunctionLTR` (345) | "Path T-Junction S" | `'╦'` | `Palette.PathTan` |
| `DirtPath.TJunctionTBR` (299) | "Path T-Junction W" | `'╣'` | `Palette.PathTan` |
| `DirtPath.TJunctionLTB` (451) | "Path T-Junction E" | `'╠'` | `Palette.PathTan` |
| `DirtPath.EndTop` (244) | "Path Dead End N" | `'╨'` | `Palette.PathTan` |
| `DirtPath.EndBottom` (243) | "Path Dead End S" | `'╥'` | `Palette.PathTan` |
| `DirtPath.EndRight` (359) | "Path Dead End W" | `'╡'` | `Palette.PathTan` |

---

## 5. Renderer Changes

### 5.1 `AsciiMapRenderer` — Per-Tile Colouring

**Current issue:** [`DrawTileRows`](Veldrath.Client/Rendering/AsciiMapRenderer.cs:99) builds an entire row into one `StringBuilder`, then renders it with a single monochrome `darkBrush`. The `TileAsciiMap.GetColor()` data exists but cannot be applied.

**Change:** Replace the per-row approach with per-tile `FormattedText` rendering:

```csharp
private void DrawTileRows(DrawingContext context, RenderState s, int vpW, int vpH) { ... }
```

New approach:
- Iterate tiles individually (not per-row strings)
- For each tile, look up `TileRegistry.Get(tileIndex)`
- Create a `FormattedText` with `descriptor.AsciiChar`, `descriptor.Foreground` color brush
- Optionally draw a background rectangle using `descriptor.Background`
- Cache `SolidColorBrush` instances for each foreground color to avoid allocations

**Performance consideration:** For a typical viewport of ~26×17 = 442 tiles, creating 442 `FormattedText` instances per frame at 30fps is ~13,260 allocations/sec. This is well within .NET's GC budget for short-lived objects. Brushes are cached as `static readonly` singletons per color slot.

**Entity rendering change:** Replace the hardcoded switch with `EntityAppearanceRegistry.Get(entity.SpriteKey, entity.EntityType)`.

### 5.2 `SpriteMapRenderer` — Optional TileMetadata Decorator

The sprite renderer already has its own rendering pipeline via `TileTextureCache`. It doesn't need to change visually. Optionally, it could consume `TileRegistry` for:
- Showing tile names in a debug overlay or tooltip
- Using `TileCategory` to apply category-specific visual effects (e.g., water ripple shader)

This is **out of scope** for the initial implementation but is enabled by the shared `TileRegistry`.

---

## 6. File Changes Summary

| Action | File | Notes |
|--------|------|-------|
| **Create** | `TileCategory.cs` | New enum |
| **Create** | `TileDescriptor.cs` | New record struct |
| **Create** | `TileRegistry.cs` | Static lookup, replaces `TileAsciiMap` |
| **Create** | `AsciiPalette.cs` | Named colour slots + cached brushes |
| **Create** | `EntityAppearance.cs` | New record struct |
| **Create** | `EntityAppearanceRegistry.cs` | Static lookup for entity → char/colour |
| **Delete** | `TileAsciiMap.cs` | Replaced by `TileRegistry` |
| **Modify** | `AsciiMapRenderer.cs` | Per-tile colouring, entity registry, palette references |
| **Modify** | `MapRendererResolver.cs` | If `AsciiMapRenderer` DI ctor changes |
| **Modify** | `App.axaml.cs` | If DI registration changes |

**Files NOT modified:**
- `IMapRenderer.cs` — interface unchanged
- `RenderState.cs` — unchanged
- `SpriteMapRenderer.cs` — unchanged (but *could* consume `TileRegistry` later)
- `TileTextureCache.cs`, `EntityTextureCache.cs` — unchanged
- `TilemapControl.cs`, `RegionTilemapControl.cs` — unchanged
- All engine, server, and contracts files — unchanged

---

## 7. Migration Path

### Phase 1: Create new types (zero behaviour change)
1. Create `TileCategory.cs`
2. Create `TileDescriptor.cs`
3. Create `AsciiPalette.cs`
4. Create `TileRegistry.cs` — populate from the mapping table (Section 4)
5. Create `EntityAppearance.cs`
6. Create `EntityAppearanceRegistry.cs` — populate with defaults

Build: should compile clean. Old `TileAsciiMap` still present, no callers changed yet.

### Phase 2: Refactor `AsciiMapRenderer` to use new system
1. Change `DrawTileRows` from per-row string to per-tile `FormattedText` with colour
2. Replace `TileAsciiMap.GetChar(tileIndex)` with `TileRegistry.Get(tileIndex)` → `.AsciiChar`, `.Foreground`, `.Background`
3. Replace hardcoded entity switch with `EntityAppearanceRegistry.Get(entity.SpriteKey, entity.EntityType)`
4. Replace all hardcoded `new SolidColorBrush(Color.FromRgb(...))` with `AsciiPalette.*Brush` properties
5. Use `AsciiPalette.*` colour constants for minimap, fog, highlights, labels

### Phase 3: Clean up
1. Delete `TileAsciiMap.cs` (all references migrated)
2. Verify build: `dotnet build Veldrath.slnx` — 0 errors, 0 warnings
3. Verify tests: `dotnet test Veldrath.Client.Tests` — all passing
4. Verify CS1591 compliance

---

## 8. Future Expansion Points (Out of Scope)

These are enabled by the new architecture but not implemented initially:

1. **Per-layer ASCII drawing** — `TileLayerDto.ZIndex` could drive layer-aware character selection (e.g., base layer water = `≈` blue bg, objects layer canopy = `"` green)
2. **Tile animation** — `TileDescriptor` could carry an animation sequence (multiple chars/colours cycling on a timer) for water shimmer, torch flicker
3. **JSON-configurable tiles** — `TileRegistry` could load from `appsettings.json` or a `tiles.json` file instead of being hardcoded
4. **Themes** — `AsciiPalette` could become an injectable interface (`IAsciiPalette`) with implementations for dark/light/high-contrast
5. **Minimap terrain** — use `TileRegistry.Get(tileIndex).Category` to show terrain-coloured dots on the minimap instead of just `#`/`.`
6. **Tooltip integration** — `SpriteMapRenderer` could show `TileDescriptor.Name` on hover

---

## 9. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Per-tile `FormattedText` allocation overhead at 30fps | Benchmark first. 442 allocs/frame × 30fps = 13,260/s — trivial for .NET GC. Brushes are cached `static readonly`. |
| Unicode box-drawing chars may not render in all monospace fonts | Test with `JetBrains Mono`, `Cascadia Code`, `Consolas`. Provide fallback characters in `TileRegistry`. |
| `AsciiPalette` static properties could be accidentally mutated | `SolidColorBrush` is immutable after construction. `Color` is a value type. Use `{ get; }` (not `{ get; set; }`). |
| `TileRegistry` static constructor firing order | No dependency on other static constructors beyond `AsciiPalette` (which has no ctor). Safe. |
| Entity appearance registry doesn't know about all possible sprite keys | Graceful fallback: unknown `spriteKey` → entity type fallback → `'?'` debug char. Easily extensible via `Register()`. |
