---
kind: work-item
date: 2026-04-11
area: web
title: "Auth flow hardening (pass 1)"
---

### Session-31 (2026-04-11) — Auth flow hardening (pass 1)

**Goal**: Multi-pass iterative auth bug fixing across Veldrath.Web, RealmFoundry, Veldrath.Server.

**Changes**:

1. **`Veldrath.Web/Components/Layout/MainLayout.razor`** — `OnLocationChanged` was `async void`, causing cross-thread state corruption. Fixed to `_ = InvokeAsync(async () => { if (AuthState.IsLoggedIn) await AuthState.TryRefreshAsync(); });`.

2. **`Veldrath.Web/Services/AuthStateService.cs`** — `TryRefreshAsync` left `IsLoggedIn = true` when `RenewJwtAsync` returned null. Now calls `await LogOutAsync()` before returning false — clears stale auth state.

3. **`RealmFoundry/Services/AuthStateService.cs`** — Same auto-logout fix. Also added `TimeSpan? expiryThreshold = null` parameter with early-exit guard (default 2-minute threshold) so `TryRefreshAsync()` doesn't hammer the API on every navigation when token is still fresh.

4. **`Veldrath.Server/Features/Auth/AuthService.cs`** — Added `ILogger<AuthService>` ctor parameter (8th param). Added structured `LogWarning` calls for: failed login (account not found / wrong password), lockout, refresh token theft detection, RenewJwt with revoked token, OAuth account creation failure. Added `LogInformation` for pending link confirmation email dispatch.

5. **`Veldrath.Server/Features/Auth/AuthEndpoints.cs`** — `POST /api/auth/logout` gained `.RequireRateLimiting("auth-attempts")` to prevent DoS via revocation flooding.

6. **`Veldrath.Server/Program.cs`** — Added startup guard: throws `InvalidOperationException` when `Auth:CookieDomain` is not configured in **Production** (`app.Environment.IsProduction()`). **Important**: guard uses `IsProduction()` not `!IsDevelopment()` — the WebAppFactory uses `"Test"` environment name and `!IsDevelopment()` would throw there too.

**Tests added/modified**:
- `RealmFoundry.Tests/Services/AuthStateServiceTests.cs`: 3 new threshold guard tests; existing 2 affected tests now use 30-second expiry tokens (so threshold guard doesn't short-circuit the API call). Total: **51 tests**.
- `RealmFoundry.Tests/Infrastructure/FakeApiClient.cs`: Added `RenewJwtCallCount` property.
- `Veldrath.Web.Tests/Services/AuthStateServiceTests.cs`: Added `IsLoggedIn.Should().BeFalse()` assertion to `TryRefreshAsync_ReturnsFalse_WhenRenewFails`.

**Final counts**: Client 713 / Server 728 / RealmFoundry.Tests 51 / Veldrath.Web.Tests 31 — all passing.

**Still excluded (future passes)**:
- `AccountLinkService.RequestLinkAsync` idempotency (duplicate token on second request)
- `PendingLinkEndpoints` rate limiting
- `ExternalAuthEndpoints` link-mode OAuth integration tests
- `LoginViewModel` external OAuth flow tests

---
