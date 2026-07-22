# Auth Flow & Character Creation Gap Fix Plan

> **Status**: Updated through Session-41 (2026-06-30). Auth hardening Pass 1+2 complete. Settings system orphaned files deleted. Full test suite verified green at 4,739 tests.

**Started:** 2026-04-02
**Status:** ✅ Complete — all 4 previously-failing CharacterCreationSessionEndpointTests now passing (verified in Session-41 full test run)

---

## Decisions Made

- **CC-P3-1 Starting location:** Static hardcode — all new characters start at `"fenwick-market"` (implemented as `private const string DefaultStartingLocationSlug` in `CharacterCreationSessionEndpoints.cs`)
- **P1-4 Token refresh failure:** Auto-disconnect + redirect to main menu; existing offline/disconnected UX handles recovery from there

---

## Phase 0: Slug Normalization — ✅ DONE

File: `RealmEngine.Data/Seeders/ZoneLocationsSeeder.cs`

| Old slug | New slug | Connection rows updated |
|---|---|---|
| `the-wayward-pilgrim` | `wayward-pilgrim` | 2 connection rows |
| `the-grey-cup` | `grey-cup` | 2 connection rows |
| `fire-ancient-chamber` | `fire-ancients-chamber` | 3 connection rows |

---

## Phase 1: Structural Gap Fixes — ✅ DONE

- Static starting location (`fenwick-market`) implemented in `CharacterCreationSessionEndpoints.cs`
- Auto-disconnect on token refresh failure implemented (`ConnectionLost` event on `IServerConnectionService`, wired in `GameViewModel` and `CharacterSelectViewModel`)
- All associated tests pass

## Phase 2: P1 Test Coverage + Auth Hardening Pass 2 — ✅ DONE

- Auth tests and character creation tests complete (lockout, preview, PATCH, Finalize, cross-session, double-finalize, StepError paths)
- Auth Hardening Pass 2 complete (Sessions 35-37, 2026-04-16 to 2026-04-18): forgot password flow, account profile management, session persistence, LinkedAt timestamp, shared auth libraries (`Veldrath.Auth`, `Veldrath.Auth.Blazor`, `Veldrath.Auth.Tests`), enhanced auth infrastructure (OAuth renewal, logout revocation, JWT renewal, refresh token rotation)
- Items excluded from Pass 2 (pending for future passes): `AccountLinkService.RequestLinkAsync` idempotency, `PendingLinkEndpoints` rate limiting, OAuth link-mode integration tests, `LoginViewModel` external OAuth flow tests

---


## ✅ Failing Tests — RESOLVED

**All 4 `CharacterCreationSessionEndpointTests` are now passing** — verified in Session-41 full test run. Full suite is fully green at 4,739 tests across all 11 test projects, 0 failures.

The 4 previously-failing tests and their fixes:
- **`Finalize_AlreadyFinalizedSession_Returns400`**: Added session-status guard before name-conflict check in `CharacterCreationSessionEndpoints.cs`
- **`Finalize_Sets_StartingLocationSlug_To_FenwickMarket`**: Changed assertion from HTTP GET to direct `ApplicationDbContext` query
- **`PatchAttributes_ValidPointBuy_Returns200`**: Fixed request body shape to use `Allocations` wrapper key
- **`GetPreview_AfterBegin_Returns200`**: Selects class before calling preview in test setup

Additionally, two other fixes were applied in Session-41:
- **`ExternalAuthEndpointTests`**: Added `SecurityStamp` to test user creation (was throwing `InvalidOperationException: User security stamp cannot be null`)
- **`EnemyAiService`**: Added disposal-state guard in `StopAsync`/`Dispose` to fix `ObjectDisposedException: The CancellationTokenSource has been disposed`

> **Note (Session-41, 2026-06-30):** These 4 CharacterCreationSessionEndpointTests were previously documented as failing in Session-31/39. They have now been verified passing via the full `dotnet test Realm.Full.slnx` run. No remaining test failures exist in the suite.
