# ASCII Tile & Entity System — Comprehensive Design (SUPERSEDED)

> **Status**: Superseded by the Pure Reactive UI pivot ([`reactive-ui-pivot-plan.md`](reactive-ui-pivot-plan.md)).
> The ASCII/sprite rendering layer has been removed in favor of a full panel-driven reactive UI.
> This document is retained for historical reference only.

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
