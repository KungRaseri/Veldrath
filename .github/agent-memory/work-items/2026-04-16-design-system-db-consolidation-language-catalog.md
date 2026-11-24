---
kind: work-item
date: 2026-04-16
area: engine
title: "Design System, DB Consolidation, Language Catalog"
---

### Session-36 (2026-04-16) — Design System, DB Consolidation, Language Catalog
- Created comprehensive design system document (`docs/design-system.md`, 726 lines)
- Created `RealmUI.Fonts` project with Cinzel, JetBrains Mono, Lora fonts
- Enhanced ForgeTheme, GameTheme with VDS design tokens
- Updated 18 Avalonia views and 14 Blazor pages for consistent theming
- Updated CSP for Google Fonts
- Consolidated database migrations: removed 10+ old migrations, replaced with single `InitialCreate` per DbContext
- Added language catalog system: `ILanguageRepository`, `EfCoreLanguageRepository`, `GetLanguageCatalogQuery`
- Language endpoints + tests added
- Updated `toc.yml` for DocFX documentation
