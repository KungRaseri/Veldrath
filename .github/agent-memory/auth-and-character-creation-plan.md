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

### Step 1 — Static Starting Location
`Veldrath.Server/Features/Characters/CharacterCreationSessionEndpoints.cs`
- Added `private const string DefaultStartingLocationSlug = "fenwick-market";`
- `FinalizeAsync`: `CurrentZoneLocationSlug = DefaultStartingLocationSlug`

### Step 2 — Auto-disconnect on token refresh failure

**a) `Veldrath.Client/Services/ServerConnectionService.cs`**
- Added `event Action? ConnectionLost` to `IServerConnectionService` interface
- `ServerConnectionService`: fires `ConnectionLost?.Invoke()` from the `Closed` handler
- `AccessTokenProvider`: checks `RefreshAsync()` return — if false, logs warning and returns `null`

**b) `Veldrath.Client/ViewModels/GameViewModel.cs`**
- Constructor subscribes `_connection.ConnectionLost += OnConnectionLost`
- `OnConnectionLost()`: `_audioPlayer?.StopMusic(); _navigation.NavigateTo<MainMenuViewModel>();`

**c) `Veldrath.Client/ViewModels/CharacterSelectViewModel.cs`**
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

### Auth Hardening Pass 2 — Sessions 35-37 (2026-04-16 to 2026-04-18)

**Completed Work:**

1. **Forgot Password Flow** — Full implementation across client (Avalonia), web (Blazor SSR), and server (API endpoints):
   - `ForgotPasswordViewModel.cs` (+66 lines), `ForgotPasswordView.axaml` (+81 lines)
   - `ForgotPassword.razor` (+71 lines), `ResetPassword.razor` (+87 lines)
   - `AuthService.cs` — +54 lines for password reset/confirmation flows
   - `ForgotPasswordViewModelTests.cs` (+83 lines)

2. **Account Profile Management**:
   - `Profile.razor` (+444 lines) — comprehensive account management UI
   - Linked accounts display, active session management
   - `IVeldrathAuthApiClient`/`VeldrathAuthApiClient` extended for profile operations

3. **Session Persistence**:
   - Login and registration flows now persist sessions
   - `TokenStore.cs` enhanced with roles, permissions, session ID tracking
   - `TokenPersistenceService.cs` for token storage

4. **LinkedAt Timestamp**:
   - `PlayerUserLogin.cs` entity extending `IdentityUserLogin` with `LinkedAt`
   - EF Migration `20260418013500_AddLinkedAtToUserLogins`
   - `AccountLinkService`, `ExternalAuthEndpoints`, `AuthService`, `AccountService` all updated to track linked date

5. **Shared Auth Libraries**:
   - `Veldrath.Auth` project with `IVeldrathAuthApiClient`/`VeldrathAuthApiClient`
   - `Veldrath.Auth.Blazor` project with `AuthStateServiceBase`
   - `Veldrath.Auth.Tests` project (436+ lines of tests)
   - `RealmFoundry` and `Veldrath.Web` now inherit from shared base classes

6. **Enhanced Auth Infrastructure**:
   - OAuth token refresh silent renewal before redirect
   - Logout with server session revocation
   - JWT renewal endpoint
   - Refresh token rotation middleware
   - Enhanced error handling in auth API client
   - Improved OAuth local listener with port binding retry

**Items Excluded from Pass 2 (still pending for future passes):**
- `AccountLinkService.RequestLinkAsync` idempotency — duplicate token on second request
- `PendingLinkEndpoints` rate limiting
- `ExternalAuthEndpoints` link-mode OAuth integration tests
- `LoginViewModel` external OAuth flow tests

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
