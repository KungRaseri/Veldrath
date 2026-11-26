---
kind: work-item
date: 2026-10-25
area: engine
title: "A6: hardcore memorial and leaderboard (Milestone A complete)"
---

### Session-89 (2026-10-25) — A6: hardcore memorial and leaderboard (Milestone A complete)

**No schema change** — `DeletedAt` + `DifficultyMode` already carried everything. `ICharacterRepository` gained
`GetMemorialAsync(accountId)` (soft-deleted **hardcore** for one account, newest death first, sorted **client-side**
because the SQLite provider cannot `ORDER BY` a `DateTimeOffset`) and `GetHardcoreLeaderboardAsync(skip, take)`
returning `(Items, Total)` so the caller can paginate without a second query (level desc, experience desc, name asc).

**Endpoints** (`CharacterEndpoints`): `GET /api/characters/memorial` and a separate authorized group
`GET /api/leaderboard/hardcore?page=&pageSize=` (page ≥ 1, pageSize clamped 1–100) which marks the caller's own rows
with `isOwn` and includes `diedAt` for the fallen.

**Web:** `fetchMemorial` / `fetchHardcoreLeaderboard` in `#lib/server/character-api.ts`; the roster page's loader now
fetches the memorial and renders a "Memorial" section (only when non-empty); new route
`routes/leaderboard/hardcore` with a paged board, `You` chips, a skull for the fallen and honest empty state.
Everything is fetched server-side through `gameApiFetch` — no client-side tokens.

**Acceptance that was already true:** `CharacterRepository.GetByIdAsync` filters `DeletedAt == null`, so a soft-deleted
hardcore character can be neither selected nor respawned; that is now **asserted** (`A_Soft_Deleted_Character_Can_No_Longer_Be_Loaded_For_Play`) instead of assumed. Respawn's hardcore refusal was already tested.

**Traps:** `Characters` has `UNIQUE (AccountId, SlotIndex)`, so multi-character seeders must allocate the next free slot
(`Count(existing) + 1`) rather than hardcoding `SlotIndex = 1`; and the SQLite `DateTimeOffset` ordering limitation
bites any new query that sorts on `DeletedAt`/`LastPlayedAt`.

**Tests:** `CharacterMemorialTests` (4: memorial filtering/order, board ordering incl. the fallen, pagination totals,
deleted characters not loadable). Server 803 → **807**, full solution **3,952**, Vitest 185, web typecheck/lint/build
and `dotnet format` clean.

**Milestone A is complete:** A1–A6 all closed; epic #38 can be closed.
