# Auth Flow & Character Creation Gap Fix Plan

**Started:** 2026-04-02  
**Status:** In Progress — paused mid-test-verification

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

### Step 1 — Static Starting Location
`RealmUnbound.Server/Features/Characters/CharacterCreationSessionEndpoints.cs`
- Added `private const string DefaultStartingLocationSlug = "fenwick-market";`
- `FinalizeAsync`: `CurrentZoneLocationSlug = DefaultStartingLocationSlug`

### Step 2 — Auto-disconnect on token refresh failure

**a) `RealmUnbound.Client/Services/ServerConnectionService.cs`**
- Added `event Action? ConnectionLost` to `IServerConnectionService` interface
- `ServerConnectionService`: fires `ConnectionLost?.Invoke()` from the `Closed` handler
- `AccessTokenProvider`: checks `RefreshAsync()` return — if false, logs warning and returns `null`

**b) `RealmUnbound.Client/ViewModels/GameViewModel.cs`**
- Constructor subscribes `_connection.ConnectionLost += OnConnectionLost`
- `OnConnectionLost()`: `_audioPlayer?.StopMusic(); _navigation.NavigateTo<MainMenuViewModel>();`

**c) `RealmUnbound.Client/ViewModels/CharacterSelectViewModel.cs`**
- Constructor subscribes `_connection.ConnectionLost += OnConnectionLost`
- `OnConnectionLost()`: `_navigation.NavigateTo<MainMenuViewModel>()`
- `DoProactiveTokenRefreshAsync`: calls `await _connection.DisconnectAsync()` before NavigateTo on failed refresh

---

## Phase 2: P1 Test Coverage — PARTIALLY DONE

### Auth Tests

| Step | Description | Status | File |
|---|---|---|---|
| 3 | `AccessTokenProvider_Should_Return_Null_When_RefreshAsync_Fails` | ✅ | `ServerConnectionServiceTests.cs` |
| 3b | `AccessTokenProvider_Should_Fire_ConnectionLost_Via_Closed_When_Refresh_Fails` | ✅ | `ServerConnectionServiceTests.cs` |
| 4 | `Refresh_Should_Return_Unauthorized_For_Expired_Token` | ✅ | `AuthEndpointTests.cs` |
| 5 fix | Lockout 401 distinct from wrong-password 401 in `AuthService.ReadErrorAsync` | ✅ | `AuthService.cs` |
| 5 server test | `Login_Should_Return_Lockout_Message_After_Max_Failed_Attempts` | ✅ | `AuthEndpointTests.cs` |
| 5 client test | `LoginAsync_Should_Return_Lockout_Message_When_Server_Returns_Locked_Out` | ✅ | `HttpServiceTests.cs` |

### Character Creation Tests

| Step | Description | Status | File |
|---|---|---|---|
| 6 | `GET /preview` tests | ✅ committed | `CharacterCreationSessionEndpointTests.cs` |
| 7 | PATCH species/background/attributes/equipment (200 + 404) | ✅ committed | `CharacterCreationSessionEndpointTests.cs` |
| 8 | FinalizeAsync branches (no species, no bg, no name, invalid mode, hardcore, duplicate name, fenwick-market slug) | ✅ committed | `CharacterCreationSessionEndpointTests.cs` |
| 10 | Cross-session 403 tests | ✅ committed | `CharacterCreationSessionEndpointTests.cs` |
| 11 fix | Double-finalize guard in `FinalizeCreationSessionHandler` | ✅ | `FinalizeCreationSessionCommand.cs` |
| 11 test | `Finalize_AlreadyFinalizedSession_Returns400` | ✅ committed | `CharacterCreationSessionEndpointTests.cs` |
| 9 | `DoNextStepAsync` StepError paths steps 0–5 | ✅ committed | `CreateCharacterViewModelTests.cs` |

---

## ⚠️ Failing Tests — PAUSE STATE

**4 `CharacterCreationSessionEndpointTests` tests are failing.** Build is clean (0 errors), failures are logic/API behavior mismatches.

### Failure 1: `Finalize_AlreadyFinalizedSession_Returns400`
- **Expected:** 400 BadRequest
- **Actual:** 409 Conflict
- **Cause:** The second finalize call hits `NameExistsAsync` (name was already saved by first call) before it can hit the finalized-status guard. The `FinalizeCreationSessionHandler` guard is never reached because `FinalizeAsync` in the endpoint already returns 409 from the repo check.
- **Fix needed:** The endpoint's `FinalizeAsync` needs to check session status **before** the name-conflict check. Add `if (session.Status == CreationSessionStatus.Finalized) return Results.BadRequest(...)` in `CharacterCreationSessionEndpoints.cs` after the ownership check, before the class/species/background checks.
  - Requires adding `using RealmEngine.Shared.Models;` to the endpoints file.

### Failure 2: `Finalize_Sets_StartingLocationSlug_To_FenwickMarket`
- **Expected:** GET `/api/characters/{id}` returns 200 with "fenwick-market" in body
- **Actual:** 405 Method Not Allowed
- **Cause:** `GET /api/characters/{id}` does not exist — `CharacterEndpoints` only maps `GET /` (list), `POST /` (create), `DELETE /{id}` (delete). There is no get-by-id endpoint.
- **Fix needed:** Change the assertion. Instead of calling a missing endpoint, verify via `factory.Services.CreateScope()` → `ApplicationDbContext.Characters` → find by `character.Id` → assert `CurrentZoneLocationSlug == "fenwick-market"`. Remove the HTTP GET assertion.

### Failure 3: `PatchAttributes_ValidPointBuy_Returns200`
- **Expected:** 200 OK
- **Actual:** 500 Internal Server Error
- **Cause:** The test sends individual stat fields (`Strength`, `Dexterity`, etc.) as a flat object, but the endpoint expects `SetCreationAttributesRequest` which wraps them in an `Allocations` dictionary: `{ "Allocations": { "Strength": 14, ... } }`.
- **Fix needed:** Wrap the body in an `Allocations` key: `new { Allocations = new { Strength = 14, Dexterity = 12, Constitution = 13, Intelligence = 8, Wisdom = 10, Charisma = 8 } }`.

### Failure 4: `GetPreview_AfterBegin_Returns200`
- **Expected:** 200 OK
- **Actual:** 400 BadRequest
- **Cause:** `GetPreviewAsync` returns 400 when `result.Character is null`. After only `BeginSession`, no class is set so the preview result has no character data. The preview query returns failure/null if no class is selected yet.
- **Fix needed:** Either (a) select a class before calling preview, or (b) only assert the status is not 404 (session exists) rather than 200. The cleaner fix is to set a class first before calling preview, consistent with how preview works.

---

## Continuation Instructions

1. Fix the 4 failing tests in `CharacterCreationSessionEndpointTests.cs` (details above)
2. Run `dotnet test RealmUnbound.slnx --filter "Category!=UI" --no-build` — expect all pass
3. Run `dotnet test Realm.Full.slnx --filter "Category!=UI" --no-build` — verify full suite

### Required usings to add to `CharacterCreationSessionEndpoints.cs` for Fix 1
```csharp
using RealmEngine.Shared.Models;
```

### Fix 1 code location
In `FinalizeAsync`, after the ownership check and before the class-selection check:
```csharp
if (session.Status == CreationSessionStatus.Finalized)
    return Results.BadRequest(new { error = "Session has already been finalized." });
```

### Fix 2 code (replace the `Finalize_Sets_StartingLocationSlug_To_FenwickMarket` test assertion block)
```csharp
response.StatusCode.Should().Be(HttpStatusCode.Created);
var character = await response.Content.ReadFromJsonAsync<CharacterDto>();
using var scope = _factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var persisted = await db.Characters.FirstAsync(c => c.Id == character!.Id);
persisted.CurrentZoneLocationSlug.Should().Be("fenwick-market");
```
Requires adding `using Microsoft.EntityFrameworkCore;` and `using RealmUnbound.Server.Data;` to the test file (already present from AuthEndpointTests pattern — verify they are in this file's usings).

### Fix 3 code (new body shape for PatchAttributes test)
```csharp
new { Allocations = new Dictionary<string, int>
{
    ["Strength"] = 14, ["Dexterity"] = 12, ["Constitution"] = 13,
    ["Intelligence"] = 8, ["Wisdom"] = 10, ["Charisma"] = 8
}}
```

### Fix 4 code (select class before calling preview)
```csharp
await client.PatchAsJsonAsync(
    $"/api/character-creation/sessions/{begin.SessionId}/class",
    new { ClassName = "warrior" });
// then call GetAsync /preview
```
Or simplify: merge `GetPreview_AfterBegin_Returns200` with `GetPreview_AfterClassSelected_ReturnsPreviewWithClassName` and remove the bare-begin test since it always fails.
