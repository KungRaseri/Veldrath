---
kind: area
date: 2026-03-30
area: engine
title: "Phase 0b — Server Schema Migrations (2026-03-30)"
---

## Phase 0b — Server Schema Migrations (2026-03-30)

Three migrations added to `Veldrath.Server/Data/Migrations/Application/`:

- `20260330000001_AddCharacterDifficultyMode` — adds `DifficultyMode string` (default `"Normal"`) to `Characters` table
- `20260330000002_AddZoneRescueFund` — adds `RescueFundTotal long` (default `0`) to `Zones` table
- `20260330000003_AddGlobalStats` — new `GlobalStats` table; `GlobalStat` entity with `Key string PK, Value long`

Entity changes:
- `Veldrath.Server/Data/Entities/Character.cs`: `public string DifficultyMode { get; set; } = "Normal";`
- `Veldrath.Server/Data/Entities/Zone.cs`: `public long RescueFundTotal { get; set; } = 0;`
- `Veldrath.Server/Data/Entities/GlobalStat.cs`: new entity
- `ApplicationDbContext.GlobalStats DbSet<GlobalStat>` registered

Server.Tests: 468 passing (unchanged — schema picked up automatically via `EnsureCreated` in SQLite integration tests).
