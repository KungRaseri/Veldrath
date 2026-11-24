---
kind: work-item
date: 2026-10-25
area: engine
title: "Dead-service sweep, StatusEffectParser hardening, tracker emptied"
---

### Session-73 (2026-10-25) — Dead-service sweep, StatusEffectParser hardening, tracker emptied

**Tracker:** all five legacy epics (#3–#7) closed with rationale comments; the web-first rewrites
live in [`plans/github-issue-refresh.md`](../../../plans/github-issue-refresh.md) until the next
milestone is chosen. No open issues. GitHub MCP has **no label endpoints** — labels must be created
in the UI.

**Deleted as dead code (three commits, `63688c19` / `f860bfa1` / `f6ef5deb`):**
- `RespawnService` — never injected. The live path is `GameHub.Respawn` → `RespawnHubCommandHandler`,
  which only restores a portion of HP + full mana. **There is no death cost today** (no XP/gold loss,
  no death sickness, no blessing pick). The deleted service had that logic, unwired.
- `CharacterInitializationService` — duplicated by the live
  `InitializeStartingAbilitiesCommand`/`Handler` (dispatched from `CreateCharacterHandler`).
- `EquipmentPowerService` (`RealmEngine.Shared/Services/EquipmentAbilityService.cs`) — the only writer
  of `Character.EquipmentGrantedAbilities`, and nothing called it. Property plus `HasAbility` /
  `GetAvailableAbilityIds` removed with it; all live ability checks read `LearnedAbilities` directly.
  Equipment granting powers is therefore an *unbuilt feature*, not a broken one.

**`StatusEffectParser` fixes:** a `statusDuration` override is now honoured only when it parses to a
positive number (0/-1 used to create an effect that expired on the turn it landed); `statModifiers`
is read from `JsonElement` objects (JSON-authored content always arrives that way) as well as both
dictionary shapes; custom stat names are lower-cased to match the `attack`/`defense` keys
`CombatService` looks up. `StatusEffectParserTests` went 70 → 75.

**Docs:** removed the `web:desktop` VS Code task and the `build:desktop`/`adapter-static` lines in the
`web-development` skill; `plans/README.md` now marks the decommission plan ✅ Complete (W1–W12).
`scripts/dev/` is fully untracked scratch — its `desktop-static-server.py` / `verify-desktop.mjs` are
not repo debt.

**Verified:** `dotnet build Realm.Full.slnx` clean, `dotnet test Realm.Full.slnx` = **3,841 passed /
0 failed** (Core 2021, Shared 793, Server 696, Data 265, Discord 29, Assets 10, Forge 27);
`dotnet format --verify-no-changes` clean; `scripts/check-markdown-links.mjs` clean over 39 files.
