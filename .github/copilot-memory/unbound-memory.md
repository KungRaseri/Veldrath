# RealmUnbound Server + Client — Memory Notes

## Architecture: Hub → MediatR Bridge Pattern

`SelectCharacter` and `EnterZone` are **session management** — they do NOT call `mediator.Send`. The character tracker and zone session repository ARE the implementation. This is intentional.

All gameplay operations follow this pattern:
1. Create `Features/{Feature}/{Name}HubCommand.cs` — command record + result record + handler class
2. Handler reads/writes the server `Character.Attributes` JSON blob (NOT the Core domain `Character`)
3. Add hub method in `GameHub.cs`: `TryGetCharacterId` guard → `mediator.Send` → zone-group broadcast or caller fallback
4. Add `ReactiveCommand` + callback on `GameViewModel`, wire hub subscription in `CharacterSelectViewModel`

**CRITICAL**: `RealmUnbound.Server.Data.Entities.Character` ≠ `RealmEngine.Shared.Models.Character`. Hub handlers work ONLY with the server EF entity and its JSON blob — they never call Core handlers directly.

## Wired Hub→MediatR Bridges

| Hub Method | HubCommand | Session |
|---|---|---|
| `GainExperience` | `GainExperienceHubCommand` | 2026-03-16 |
| `RestAtLocation` | `RestAtLocationHubCommand` | 2026-03-19 session-2 |
| `AllocateAttributePoints` | `AllocateAttributePointsHubCommand` | 2026-03-19 session-3 |
| `UseAbility` | `UseAbilityHubCommand` | 2026-03-19 session-4 |

## Character Attributes JSON Blob Schema

- `UnspentAttributePoints`: int — spent via `AllocateAttributePoints`
- `Strength`, `Dexterity`, etc.: int — core stats
- `Gold`: int — currency; deducted by `RestAtLocation` (default cost: 10)
- `CurrentHealth`, `MaxHealth`: int — restored to max by `RestAtLocation`
- `CurrentMana`, `MaxMana`: int — restored to max by `RestAtLocation`
- Defaults when absent: `MaxHealth = Level * 10`, `MaxMana = Level * 5`
- Handler key constants are `internal const string KeyXxx` on the handler class

## UseAbility Handler Details (session-4)

- `DefaultManaCost = 10` deducted from `CurrentMana` per use
- Healing detected via `AbilityId.Contains("heal", OrdinalIgnoreCase)`
- Healing restores `Min(HealingAmount=25, maxHealth - currentHealth)` HP
- Broadcasts `AbilityUsed` payload: `{ CharacterId, AbilityId, ManaCost, RemainingMana, HealthRestored }`
- `AbilityUsedPayload` internal record lives in `CharacterSelectViewModel.cs`

## Test Counts

| Session | Client Tests | Server Tests | Total |
|---|---|---|---|
| Session-2 (2026-03-19) | 203 | 217 | 420 |
| Session-3 (2026-03-19) | 268 | 235 | 503 |
| Session-4 (2026-03-19) | **274** | **246** | **520** |

## P3 Stubs Status

1. ~~`MainMenuViewModel.SettingsCommand`~~ — FIXED session-3: navigates to `SettingsViewModel`
2. `CharacterSelectViewModel.ServerUrl` — still hardcoded to `"http://localhost:8080"`; needs `AppSettings` POCO or `IConfiguration` with key `GameServer:BaseUrl`
3. `SettingsViewModel` — placeholder only; no real settings form yet

## P4 XML Doc Gaps

- `IZoneRepository` + `IZoneSessionRepository` — FIXED session-4 (all 8 summaries added)
- `IPlayerAccountRepository` — still missing method summaries
- `IAuthService`, `ICharacterService`, `IContentService`, `INavigationService` (Client) — verify at next build (was clean last run)

## Next Hub Bridge Candidates

- `EquipItem` — needs server-side handler; `EquipItemCommand` takes full Core domain objects (not suitable directly)
- `CraftItem` — needs server-side handler
- `EnterDungeon` — static state gotcha: `EnterDungeonHandler._activeDungeons` is `private static Dictionary<string, DungeonInstance>`; use `ActiveDungeonScope` reflection helper in tests

## Known Gotchas

### ActorClassDto (breaking change, session-3)
- `ActorClassDto` ctor gained `HitDie` (int), `PrimaryStat` (string), `RarityWeight` (int)
- All `new ActorClassDto(slug, name, typeKey)` calls must be updated to include all 6 params

### AvailableClasses Test
- `CharacterSelectViewModel.LoadAsync` fires-and-forgets on construction; completes synchronously if `FakeContentService` returns `Task.FromResult`
- Fallback test: use `new FakeContentService { Classes = [] }` + `await Task.Delay(50)` before asserting
- Catalog-populated test: pass non-empty classes + same delay

### FakeServerConnectionService
- Tracks sent commands in `SentCommands` list (added session-3)

## Session Log

### Session-3 (2026-03-19)
- Fixed `ActorClassDto` breaking change in `FakeServices.cs` and `CharacterSelectViewModelTests.cs`
- Fixed `AvailableClasses_Should_Contain_Expected_Classes` test (empty catalog + delay pattern)
- Fixed P3 stub: `MainMenuViewModel.SettingsCommand` navigates to `SettingsViewModel`
- Created `SettingsViewModel` with `BackCommand`
- Implemented `AllocateAttributePoints` hub bridge (command + handler + hub method)
- Added 9 server tests + client tests for `AllocateAttributePoints`

### Session-4 (2026-03-19)
- Implemented `UseAbility` hub bridge end-to-end
- Fixed `IZoneRepository` + `IZoneSessionRepository` XML docs (8 summaries)
- Added 10 server tests + 6 client tests = 16 new tests
- Total: 274 client + 246 server = 520 passing
